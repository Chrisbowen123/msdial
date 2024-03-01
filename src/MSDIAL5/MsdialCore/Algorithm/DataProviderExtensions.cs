﻿using CompMs.Common.DataObj;
using CompMs.Common.Utility;
using CompMs.MsdialCore.Algorithm.Internal;
using CompMs.RawDataHandler.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompMs.MsdialCore.Algorithm;

public static class DataProviderExtensions {
    public static RawSpectrum LoadMsSpectrumFromIndex(this IDataProvider provider, int index) {
        if (index < 0) {
            return null;
        }
        return provider.LoadMsSpectrums()[index];
    }

    public static async Task<RawSpectrum> LoadMsSpectrumFromIndexAsync(this IDataProvider provider, int index, CancellationToken token) {
        if (index < 0) {
            return null;
        }
        var spectra = await provider.LoadMsSpectrumsAsync(token).ConfigureAwait(false);
        return spectra[index];
    }

    public static RawSpectrum LoadMs1SpectrumFromIndex(this IDataProvider provider, int index) {
        return provider.LoadMs1Spectrums()[index];
    }

    /// <summary>
    /// An extension method for IDataProvider to retrieve MS2 spectra that have a ScanStartTime within a specified range.
    /// </summary>
    /// <param name="provider">The data provider instance on which the extension method operates.</param>
    /// <param name="start">The lower bound of the retention time range.</param>
    /// <param name="end">The upper bound of the retention time range.</param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation and returns an array of <see cref="RawSpectrum"/> objects within the specified retention time range.</returns>
    public static async Task<RawSpectrum[]> LoadMs2SpectraWithRtRangeAsync(this IDataProvider provider, double start, double end, CancellationToken token) {
        var spectra = await provider.LoadMsNSpectrumsAsync(2, token).ConfigureAwait(false);
        var lower = spectra.LowerBound(start, (t, s) => t.ScanStartTime.CompareTo(s));
        var upper = lower;
        while (upper < spectra.Count && spectra[upper].ScanStartTime < end) {
            ++upper;
        }
        var result = new RawSpectrum[upper - lower];
        for (int i = 0; i < result.Length; i++) {
            result[i] = spectra[i + lower];
        }
        return result;
    }

    public static double GetMinimumCollisionEnergy(this IDataProvider provider) {
        return provider.LoadMsSpectrums().DefaultIfEmpty().Min(s => s?.CollisionEnergy) ?? -1d;
    }
    
    public static (int, int) GetScanNumberRange(this IDataProvider provider) {
        var spectra = provider.LoadMsSpectrums();
        return (spectra.FirstOrDefault()?.ScanNumber ?? 0, spectra.LastOrDefault()?.ScanNumber ?? 0);
    }

    public static (double, double) GetRetentionTimeRange(this IDataProvider provider) {
        var spectra = provider.LoadMsSpectrums();
        return ((float)(spectra.FirstOrDefault()?.ScanStartTime ?? 0d), (float)(spectra.LastOrDefault()?.ScanStartTime ?? 0d));
    }

    public static (double, double) GetMassRange(this IDataProvider provider) {
        var spectra = provider.LoadMsSpectrums();
        return ((float)(spectra.Min(spectrum => spectrum?.LowestObservedMz) ?? 0d), (float)(spectra.Max(spectrum => spectrum?.HighestObservedMz) ?? 0d));
    }

    public static (double, double) GetIntensityRange(this IDataProvider provider) {
        var spectra = provider.LoadMsSpectrums();
        return ((float)(spectra.Min(spectrum => spectrum?.MinIntensity) ?? 0d), (float)(spectra.Max(spectrum => spectrum?.BasePeakIntensity) ?? 0d));
    }

    public static (double, double) GetDriftTimeRange(this IDataProvider provider) {
        var spectra = provider.LoadMsSpectrums();
        return ((float)(spectra.Min(spectrum => spectrum?.DriftTime) ?? 0d), (float)(spectra.Max(spectrum => spectrum?.DriftTime) ?? 0d));
    }

    public static int Count(this IDataProvider provider) {
        return provider.LoadMsSpectrums().Count;
    }

    /// <summary>
    /// Filters the provided data provider's data based on a specified m/z value and tolerance, returning a new <see cref="IDataProvider"/> that provides access to the filtered data.
    /// </summary>
    /// <param name="provider">The original data provider to filter.</param>
    /// <param name="mz">The target m/z value to filter by.</param>
    /// <param name="tolerance">The tolerance for the m/z filtering. Data points within this tolerance from the specified m/z value will be included in the filtered data.</param>
    /// <returns>A new <see cref="IDataProvider"/> instance that provides access to the data filtered based on the specified m/z value and tolerance.</returns>
    /// <remarks>
    /// This method creates an instance of <see cref="PrecursorMzSelectedDataProvider"/>, which implements the filtering logic based on the specified m/z value and tolerance.
    /// </remarks>
    public static IDataProvider FilterByMz(this IDataProvider provider, double mz, double tolerance) {
        return new PrecursorMzSelectedDataProvider(provider, mz, tolerance);
    }

    /// <summary>
    /// Wraps the specified <see cref="IDataProvider"/> instance with a <see cref="CachedDataProvider"/>,
    /// enabling caching of spectra data to improve performance.
    /// </summary>
    /// <param name="provider">The data provider to wrap with caching functionality.</param>
    /// <returns>A new <see cref="CachedDataProvider"/> instance that caches data from the specified provider.</returns>
    /// <remarks>
    /// This method provides a convenient way to apply caching to an existing <see cref="IDataProvider"/>
    /// instance, improving data retrieval performance by storing frequently accessed spectra data in memory.
    /// </remarks>
    public static IDataProvider Cache(this IDataProvider provider) {
        return new CachedDataProvider(provider);
    }

    public static List<double> LoadCollisionEnergyTargets(this IDataProvider provider) {
        return SpectrumParser.LoadCollisionEnergyTargets(provider.LoadMsSpectrums());
    }

    public static IDataProviderFactory<object> AsFactory(this IDataProvider provider) {
        return new IdentityFactory<object>(provider);
    }

    class IdentityFactory<T>(IDataProvider provider) : IDataProviderFactory<T> {
        public IDataProvider Create(T source) => provider;
    }
}
