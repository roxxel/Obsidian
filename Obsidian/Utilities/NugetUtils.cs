using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Obsidian.Utilities
{
    public static class NugetUtils
    {
        /// <summary>
        /// Gets latest version of nuget package
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns><see cref="NuGetVersion"/> will be returned if package is found; otherwise <see langword="null"></see></returns>
        public static async Task<NuGetVersion> GetLatestVersionOfPackage(string packageId)
        {
            ILogger logger = NullLogger.Instance;
            CancellationToken cancellationToken = CancellationToken.None;

            SourceCacheContext cache = new SourceCacheContext();
            SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();

            IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(
                packageId,
                cache,
                logger,
                cancellationToken);
            return versions.LastOrDefault();
        }

        public static async Task<DownloadResourceResult> GetDownloadResourceResult(string depsFolder, SourceCacheContext cache, SourceRepository repository, DownloadResource resource, string packageId, NuGetVersion packageVersion)
        {
            return await resource.GetDownloadResourceResultAsync(
                new SourcePackageDependencyInfo(packageId, packageVersion, null, true, repository),
                new PackageDownloadContext(cache),
                depsFolder,
                NuGet.Common.NullLogger.Instance,
                CancellationToken.None);
        }

        public static void CopyFiles(PackageReaderBase packageReader, string folderPath, IEnumerable<string> files)
        {
            packageReader.CopyFiles(
                folderPath,
                files,
                ExtractFile,
                NuGet.Common.NullLogger.Instance,
                CancellationToken.None);
        }

        public static List<string> GetMatchingFiles(PackageReaderBase packageReader)
        {
            return packageReader.GetLibItems()
                .Where(x => x.TargetFramework.Framework != ".NETFramework") //we don't need .net framework here
                .Select(x => x.Items.First()).ToList();
        }

        public static string ExtractFile(string sourcePath, string targetPath, Stream sourceStream)
        {
            using (var targetStream = File.OpenWrite(targetPath))
            {
                sourceStream.CopyTo(targetStream);
            }

            return targetPath;
        }


    }
}
