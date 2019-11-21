using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;

namespace Scalar.Common
{
    public class RepoMetadata
    {
        private FileBasedDictionary<string, string> repoMetadata;
        private ITracer tracer;

        private RepoMetadata(ITracer tracer)
        {
            this.tracer = tracer;
        }

        public static RepoMetadata Instance { get; private set; }

        public string EnlistmentId
        {
            get
            {
                string value;
                if (!this.repoMetadata.TryGetValue(Keys.EnlistmentId, out value))
                {
                    value = CreateNewEnlistmentId(this.tracer);
                    this.repoMetadata.SetValueAndFlush(Keys.EnlistmentId, value);
                }

                return value;
            }
        }

        public static bool TryInitialize(ITracer tracer, string dotScalarPath, out string error)
        {
            return TryInitialize(tracer, new PhysicalFileSystem(), dotScalarPath, out error);
        }

        public static bool TryInitialize(ITracer tracer, PhysicalFileSystem fileSystem, string dotScalarPath, out string error)
        {
            string dictionaryPath = Path.Combine(dotScalarPath, ScalarConstants.DotScalar.Databases.RepoMetadata);
            if (Instance != null)
            {
                if (!Instance.repoMetadata.DataFilePath.Equals(dictionaryPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "TryInitialize should never be called twice with different parameters. Expected: '{0}' Actual: '{1}'",
                            Instance.repoMetadata.DataFilePath,
                            dictionaryPath));
                }
            }
            else
            {
                Instance = new RepoMetadata(tracer);
                if (!FileBasedDictionary<string, string>.TryCreate(
                    tracer,
                    dictionaryPath,
                    fileSystem,
                    out Instance.repoMetadata,
                    out error))
                {
                    return false;
                }
            }

            error = null;
            return true;
        }

        public static void Shutdown()
        {
            if (Instance != null)
            {
                if (Instance.repoMetadata != null)
                {
                    Instance.repoMetadata.Dispose();
                    Instance.repoMetadata = null;
                }

                Instance = null;
            }
        }

        public bool TryGetOnDiskLayoutVersion(out int majorVersion, out int minorVersion, out string error)
        {
            majorVersion = 0;
            minorVersion = 0;

            try
            {
                string value;
                if (!this.repoMetadata.TryGetValue(Keys.DiskLayoutMajorVersion, out value))
                {
                    error = "Enlistment disk layout version not found, check if a breaking change has been made to Scalar since cloning this enlistment.";
                    return false;
                }

                if (!int.TryParse(value, out majorVersion))
                {
                    error = "Failed to parse persisted disk layout version number: " + value;
                    return false;
                }

                // The minor version is optional, e.g. it could be missing during an upgrade
                if (this.repoMetadata.TryGetValue(Keys.DiskLayoutMinorVersion, out value))
                {
                    if (!int.TryParse(value, out minorVersion))
                    {
                        minorVersion = 0;
                    }
                }
            }
            catch (FileBasedCollectionException ex)
            {
                error = ex.Message;
                return false;
            }

            error = null;
            return true;
        }

        public void SaveCloneMetadata(ITracer tracer, ScalarEnlistment enlistment)
        {
            this.repoMetadata.SetValuesAndFlush(
                new[]
                {
                    new KeyValuePair<string, string>(Keys.DiskLayoutMajorVersion, ScalarPlatform.Instance.DiskLayoutUpgrade.Version.CurrentMajorVersion.ToString()),
                    new KeyValuePair<string, string>(Keys.DiskLayoutMinorVersion, ScalarPlatform.Instance.DiskLayoutUpgrade.Version.CurrentMinorVersion.ToString()),
                    new KeyValuePair<string, string>(Keys.EnlistmentId, CreateNewEnlistmentId(tracer)),
                });
        }

        public void SetEntry(string keyName, string valueName)
        {
            this.repoMetadata.SetValueAndFlush(keyName, valueName);
        }

        private static string CreateNewEnlistmentId(ITracer tracer)
        {
            string enlistmentId = Guid.NewGuid().ToString("N");
            EventMetadata metadata = new EventMetadata();
            metadata.Add(nameof(enlistmentId), enlistmentId);
            tracer.RelatedEvent(EventLevel.Informational, nameof(CreateNewEnlistmentId), metadata);
            return enlistmentId;
        }

        public static class Keys
        {
            public const string DiskLayoutMajorVersion = "DiskLayoutVersion";
            public const string DiskLayoutMinorVersion = "DiskLayoutMinorVersion";
            public const string EnlistmentId = "EnlistmentId";
        }
    }
}
