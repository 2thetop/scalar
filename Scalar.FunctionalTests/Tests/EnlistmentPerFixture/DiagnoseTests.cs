using NUnit.Framework;
using Scalar.Tests.Should;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    [NonParallelizable]
    public class DiagnoseTests : TestsWithEnlistmentPerFixture
    {
        [TestCase]
        public void DiagnoseProducesZipFile()
        {
            Directory.Exists(this.Enlistment.DiagnosticsRoot).ShouldEqual(false);
            string output = this.Enlistment.Diagnose();
            output.ShouldNotContain(ignoreCase: true, unexpectedSubstrings: "Failed");

            IEnumerable<string> files = Directory.EnumerateFiles(this.Enlistment.DiagnosticsRoot);
            files.ShouldBeNonEmpty();
            string zipFilePath = files.First();

            zipFilePath.EndsWith(".zip").ShouldEqual(true);
            output.Contains(zipFilePath).ShouldEqual(true);
        }
    }
}
