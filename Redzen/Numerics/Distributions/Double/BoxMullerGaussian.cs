﻿/* ***************************************************************************
 * This file is part of the Redzen code library.
 *
 * Copyright 2015-2020 Colin Green (colin.green1@gmail.com)
 *
 * Redzen is free software; you can redistribute it and/or modify
 * it under the terms of The MIT License (MIT).
 *
 * You should have received a copy of the MIT License
 * along with Redzen; if not, see https://opensource.org/licenses/MIT.
 */
using System;
using Redzen.Random;

namespace Redzen.Numerics.Distributions.Double
{
    /// <summary>
    /// Static methods for taking samples from Gaussian distributions using the Box-Muller transform.
    /// (see: http://en.wikipedia.org/wiki/Box_Muller_transform).
    /// </summary>
    public static class BoxMullerGaussian
    {
        #region Public Static Methods

        /// <summary>
        /// Take a sample from the standard Gaussian distribution, i.e. with mean of 0 and standard deviation of 1.
        /// </summary>
        /// <param name="rng">Random source.</param>
        /// <returns>A pair of random samples (because the Box-Muller transform generates samples in pairs).</returns>
        public static (double, double) Sample(IRandomSource rng)
        {
            // Generate two new Gaussian values.
            double x, y, sqr;

            // We need a non-zero random point inside the unit circle.
            do
            {
                x = (2.0 * rng.NextDouble()) - 1.0;
                y = (2.0 * rng.NextDouble()) - 1.0;
                sqr = (x * x) + (y * y);
            }
            while(sqr > 1.0 || sqr == 0);

            // Make the Box-Muller transformation.
            double fac = Math.Sqrt((-2.0 * Math.Log(sqr)) / sqr);

            // Return two samples.
            return (x * fac, y * fac);
        }

        /// <summary>
        /// Take a sample from the a Gaussian distribution with the specified mean and standard deviation.
        /// </summary>
        /// <param name="rng">Random source.</param>
        /// <param name="mean">Distribution mean.</param>
        /// <param name="stdDev">Distribution standard deviation.</param>
        /// <returns>A pair of random samples (because the Box-Muller transform generates samples in pairs).</returns>
        public static (double, double) Sample(IRandomSource rng, double mean, double stdDev)
        {
            var pair = Sample(rng);
            pair.Item1 = mean + (pair.Item1 * stdDev);
            pair.Item2 = mean + (pair.Item2 * stdDev);
            return pair;
        }

        /// <summary>
        /// Fill a span with samples from the standard Gaussian distribution, i.e. with mean of 0 and standard deviation of 1.
        /// </summary>
        /// <param name="rng">Random source.</param>
        /// <param name="span">The span to fill with samples.</param>
        public static void Sample(IRandomSource rng, Span<double> span)
        {
            int i=0;
            for(; i <= span.Length-2; i += 2)
            {
                var pair = Sample(rng);
                span[i] = pair.Item1;
                span[i+1] = pair.Item2;
            }

            if(i < span.Length) {
                span[i] = Sample(rng).Item1;
            }
        }

        /// <summary>
        /// Fill a span with samples from a Gaussian distribution with the specified mean and standard deviation.
        /// </summary>
        /// <param name="rng">Random source.</param>
        /// <param name="mean">Distribution mean.</param>
        /// <param name="stdDev">Distribution standard deviation.</param>
        /// <param name="span">The span to fill with samples.</param>
        public static void Sample(IRandomSource rng, double mean, double stdDev, Span<double> span)
        {
            int i=0;
            for(; i <= span.Length-2; i += 2)
            {
                var pair = Sample(rng);
                span[i] = mean + (pair.Item1 * stdDev);
                span[i+1] = mean + (pair.Item2 * stdDev);
            }

            if(i < span.Length) {
                span[i] = mean + (Sample(rng).Item1 * stdDev);
            }
        }

        #endregion
    }
}
