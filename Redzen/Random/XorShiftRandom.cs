/* ***************************************************************************
 * This file is part of the Redzen code library.
 * 
 * Copyright 2005-2018 Colin Green (colin.green1@gmail.com)
 *
 * Redzen is free software; you can redistribute it and/or modify
 * it under the terms of The MIT License (MIT).
 *
 * You should have received a copy of the MIT License
 * along with Redzen; if not, see https://opensource.org/licenses/MIT.
 */

using System;
using System.Runtime.CompilerServices;

namespace Redzen.Random
{
    /// <summary>
    /// A fast random number generator for .NET
    /// Colin Green, January 2005
    /// 
    /// 
    /// Key points:
    ///  1) Based on a simple and fast xor-shift pseudo random number generator (RNG) specified in: 
    ///  Marsaglia, George. (2003). Xorshift RNGs.
    ///  http://www.jstatsoft.org/v08/i14/paper
    ///  
    ///  This particular implementation of xorshift has a period of 2^128-1. See the above paper to see
    ///  how this can be easily extended if you need a longer period. At the time of writing I could find no 
    ///  information on the period of System.Random for comparison.
    /// 
    ///  2) Faster than System.Random. Up to 8x faster, depending on which methods are called.
    /// 
    ///  3) Direct replacement for System.Random. This class implements all of the methods that System.Random 
    ///  does plus some additional methods. The like named methods are functionally equivalent.
    ///  
    ///  4) Allows fast re-initialisation with a seed, unlike System.Random which accepts a seed at construction
    ///  time which then executes a relatively expensive initialisation routine. This provides a significant speed
    ///  improvement if you need to reset the pseudo-random number sequence many times, e.g. if you want to 
    ///  re-generate the same sequence of random numbers many times. An alternative might be to cache random numbers 
    ///  in an array, but that approach is limited by memory capacity and the fact that you may also want a large 
    ///  number of different sequences cached. Each sequence can be represented by a single seed value (int) when 
    ///  using this class.
    /// </summary>
    public sealed class XorShiftRandom : IRandomSource
    {
        // Constants.
        const double REAL_UNIT_UINT = 1.0 / (1UL << 32);
        const float REAL_UNIT_UINT_F = 1f / (1U << 24);

        // RNG state.
        uint _x, _y, _z, _w;

        #region Constructors

        /// <summary>
        /// Initialises a new instance using a seed generated from the class's static seed RNG.
        /// </summary>
        public XorShiftRandom()
        {
            Reinitialise(RandomSourceFactory.GetNextSeed());
        }

        /// <summary>
        /// Initialises a new instance using the provided ulong seed.
        /// </summary>
        public XorShiftRandom(ulong seed)
        {
            Reinitialise(seed);
        }

        #endregion

        #region Public Methods [Re-initialisation]

        /// <summary>
        /// Re-initialises the random number generator state using the provided seed value.
        /// </summary>
        public void Reinitialise(ulong seed)
        {
            // Notes.
            // The first random sample will be very strongly correlated to the value of _x we set here; 
            // such a correlation is undesirable, therefore we significantly weaken it by hashing the 
            // seed's bits using the splitmix64 PRNG.
            //
            // It is required that at least one of the state variables be non-zero;
            // use of splitmix64 satisfies this requirement because it is an equidistributed generator,
            // thus if it outputs a zero it will next produce a zero after a further 2^64 outputs.

            // Use the splitmix64 RNG to hash the seed.
            ulong t = Splitmix64Rng.Next(ref seed);
            _x = (uint)t;
            _y = (uint)(t >> 32);

            t = Splitmix64Rng.Next(ref seed);
            _z = (uint)t;
            _w = (uint)(t >> 32);
        }

        #endregion

        #region Public Methods [System.Random functionally equivalent methods]

        /// <summary>
        /// Generates a random Int32 over the interval [0, int.MaxValue), i.e. exclusive of Int32.MaxValue.
        /// </summary>
        /// <remarks>
        /// MaxValue is excluded in order to remain functionally equivalent to System.Random.Next().
        /// 
        /// For slightly improved performance consider these alternatives:
        /// 
        ///  * NextInt() returns an Int32 over the interval [0 to Int32.MaxValue], i.e. inclusive of Int32.MaxValue.
        /// 
        ///  * NextUInt(). Cast the result to an Int32 to generate an value over the full range of an Int32,
        ///    including negative values.
        /// </remarks>
        public int Next()
        {
            // Handle the special case where the value int.MaxValue is generated; this is outside 
            // the range of permitted return values for this method.
        retry:
            uint rtn = NextInner() & 0x7FFFFFFF;
            if(rtn == 0x7FFFFFFF) {
                goto retry;
            }
            return (int)rtn;            
        }

        /// <summary>
        /// Generates a random Int32 over the interval [range 0 to upperBound), i.e. excluding upperBound.
        /// </summary>
        public int Next(int upperBound)
        {
            if(upperBound < 0) {
                throw new ArgumentOutOfRangeException("upperBound", upperBound, "upperBound must be >=0");
            }

            // ENHANCEMENT: Can we do this without converting to a double and back again?
            return (int)(NextDoubleInner() * upperBound);
        }

        /// <summary>
        /// Generates a random Int32 over the interval [lowerBound, upperBound), i.e. excluding upperBound.
        /// upperBound must be >= lowerBound. lowerBound may be negative.
        /// </summary>
        public int Next(int lowerBound, int upperBound)
        {
            if(lowerBound > upperBound) {
                throw new ArgumentOutOfRangeException("upperBound", upperBound, "upperBound must be >= lowerBound");
            }

            // Test if range will fit into an Int32.
            int range = upperBound - lowerBound;
            if(range >= 0) {
                return lowerBound + (int)(NextDoubleInner() * range);
            }

            // When range is less than 0 then an overflow has occurred and therefore we must resort to using long integer arithmetic (which is slower).
            return lowerBound + (int)(NextDoubleInner() * ((long)upperBound - (long)lowerBound));
        }

        /// <summary>
        /// Generates a random double over the interval [0, 1), i.e. inclusive of 0.0 and exclusive of 1.0.
        /// </summary>
        public double NextDouble()
        {   
            return NextDoubleInner();
        }

        /// <summary>
        /// Fills the provided byte array with random bytes.
        /// </summary>
        /// <param name="buffer">The byte array to fill with random values.</param>
        public unsafe void NextBytes(byte[] buffer)
        {
            // For improved performance the below loop operates on these stack allocated copies of the heap variables.
            // Notes. doing this means that these heavily used variables are located near to other local/stack variables,
            // thus they will very likely be cached in the same CPU cache line.
            uint x=_x, y=_y, z=_z, w=_w;

            uint t;
            int i=0;

            // Get a pointer to the start of [buffer]; to do this we must pin [buffer] because it is allocated
            // on the heap and therefore could be moved by the GC at any time (if we didn't pin it).
            fixed(byte* pBuffer = buffer)
            {
                // A pointer to 32 bit size segments of [buffer].
                uint* pUInt = (uint*)pBuffer;

                // Create and store new random bytes in groups of four.
                for(int bound = buffer.Length / 4; i < bound; i++)
                {
                    // Generate 32 random bits and assign to the segment that pUInt is currently pointing to.
                    t = x ^ (x << 11);

                    x = y;
                    y = z;
                    z = w;

                    pUInt[i] = w = (w^(w>>19))^(t^(t>>8));
                }
            }

            // Fill any trailing entries in [buffer] that occur when the its length is not a multiple of four.
            // Note. We do this using safe C# therefore can unpin [buffer]; i.e. its preferable to hold pins for the 
            // shortest duration possible because they have an impact on the effectiveness of the garbage collector.

            // Convert back to one based indexing instead of groups of four bytes.
            i = i * 4;

            // Fill any remaining bytes in the buffer.
            if(i < buffer.Length)
            {
                // Generate a further 32 random bits.
                t = x ^ (x << 11);

                x = y;
                y = z;
                z = w;

                w = (w^(w>>19))^(t^(t>>8));

                // Allocate one byte at a time until we reach the end of the buffer.
                while(i < buffer.Length)
                {
                    buffer[i++] = (byte)w;
                    w >>= 8;
                }              
            }

            // Update the state variables on the heap.
            _x = x;
            _y = y;
            _z = z;
            _w = w;
        }

        #endregion

        #region Public Methods [Methods not present on System.Random]

        /// <summary>
        /// Generates a random float over the interval [0, 1), i.e. inclusive of 0.0 and exclusive of 1.0.
        /// </summary>
        public float NextFloat()
        {
            // Note. Here we generate a random integer between 0 and 2^24-1 (i.e. 24 binary 1s) and multiply
            // by the fractional unit value 1.0 / 2^24, thus the result has a max value of
            // 1.0 - (1.0 / 2^24). Or 0.99999994 in decimal.
            return (NextInner() >> 8) * REAL_UNIT_UINT_F;
        }

        /// <summary>
        /// Generates a random UInt32 over the interval [uint.MinValue, uint.MaxValue], i.e. over the full 
        /// range of a UInt32.
        /// </summary>
        public uint NextUInt()
        {
            return NextInner();
        }

        /// <summary>
        /// Generates a random Int32 over interval [0 to Int32.MaxValue], i.e. inclusive of Int32.MaxValue.
        /// </summary>
        /// <remarks>
        /// This method can generate Int32.MaxValue, whereas Next() does not; this is the only difference
        /// between these two methods. As a consequence this method will typically be slightly faster because 
        /// Next () must test for Int32.MaxValue and resample the underlying RNG when that value occurs.
        /// </remarks>
        public int NextInt()
        {
            return (int)(NextInner() & 0x7FFFFFFF);
        }

        /// <summary>
        /// Generates a random double over the interval (0, 1), i.e. exclusive of both 0.0 and 1.0
        /// </summary>
        public double NextDoubleNonZero()
        {
            // Here we generate a random value from 0 to 0ffff_fffe, and add one
            // to generate a random value from 1 to 0xffff_ffff.
            // We then multiple by the fractional unit 1.0 / 2^32.
            return ((NextInner() & 0xffff_fffe) + 1) * REAL_UNIT_UINT;
        }

        /// <summary>
        /// Generates a single random bit.
        /// </summary>
        public bool NextBool()
        {
            // Generate 32 random bits and return the most significant bit, discarding the rest.
            // This is slower than the aproach of generating and caching 32 bits for future calls, but 
            // (A) gives good quality randomness, and (B) is still very fast.
            return (NextInner() & 0x8000) == 0;
        }

        /// <summary>
        /// Generates a single random byte over the interval [0,255].
        /// </summary>
        public byte NextByte()
        {
            // Note. Explicitly masking with 0xff is unnecessary, this is achieved by the cast.
            return (byte)NextInner();
        }

        #endregion

        #region Private Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double NextDoubleInner()
        {
            // Note. Here we generate a random integer between 0 and 2^32-1 (i.e. 32 binary 1s) and multiply
            // by the fractional unit value 1.0 / 2^32, thus the result has a max value of
            // 1.0 - (1.0 / 2^32), or 0.99999999976716936 in decimal.                          
            return NextInner() * REAL_UNIT_UINT;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint NextInner()
        {
            // Generate 32 more bits.
            uint t = _x ^ (_x << 11);

            _x = _y;
            _y = _z;
            _z = _w;

            return _w = (_w^(_w>>19)) ^ (t^(t>>8));
        }

        #endregion
    }
}
