using System.Collections.Immutable;
using FluentAssertions;

namespace BLS.Test
{
    public class GlobberTests
    {
        public static IEnumerable<object[]> AllGlobberFactoryMethods
        {
            get
            {
                yield return new object[] { (IGlobberArgs args) => new SystemGlobber(args) };
                yield return new object[] { (IGlobberArgs args) => new ImprovedGlobber(args) };
                //yield break;
            }
        }

        public static IEnumerable<object[]> GlobberFactoryMethodsExcludingSystemGlobber
        {
            get
            {
                yield return new object[] { (IGlobberArgs args) => new ImprovedGlobber(args) };
                //yield break;
            }
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindSpecificFileNameWhereExists(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "FolderLevel1.txt";
            string[] expected = new[] { includeGlob };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindSpecificFileNameWhereExistsUsingSubfolderAsBase(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "../FolderLevel1.txt";
            string[] expected = new[] { includeGlob };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles/SubFolder1", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindSpecificFileNameWhereDoesNotExist(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "DoesNotExist.txt";
            string[] expected = new string[] { };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesInBaseFolder(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "*";
            string[] expected = new[]
            {
                "FolderLevel1.txt",
                "FolderLevel1.md",
                "FolderLevel1_DifferentBaseName.txt",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesInBaseFolderWithSameBaseName(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "FolderLevel1.*";
            string[] expected = new[]
            {
                "FolderLevel1.txt",
                "FolderLevel1.md",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesInBaseFolderWithSameExtension(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "*.txt";
            string[] expected = new[]
            {
                "FolderLevel1.txt",
                "FolderLevel1_DifferentBaseName.txt",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Fact]
        public void FindAllFilesInTheWindowsFolderWithSameExtension()
        {
            string includeGlob = "*.exe";
            
            ImmutableList<string> systemGlobResults = ExecuteGlob(args => new SystemGlobber(args), @"C:\Windows", includeGlob);
            ImmutableList<string> improvedGlobResults = ExecuteGlob(args => new ImprovedGlobber(args), @"C:\Windows", includeGlob);

            systemGlobResults.Should().BeEquivalentTo(improvedGlobResults);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFiles(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "**/*";
            string[] expected = new[]
            {
                "FolderLevel1.txt",
                "FolderLevel1.md",
                "FolderLevel1_DifferentBaseName.txt",
                "SubFolder1/SubFolder1_FolderLevel2.txt",
                "SubFolder1/SubFolder1_FolderLevel2.md",
                "SubFolder2/SubFolder2_FolderLevel2.txt",
                "SubFolder2/SubFolder2_FolderLevel2.md",
                "SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.txt",
                "SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesExcludingTxt(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "**/*";
            string excludeGlob = "**/*.txt";
            string[] expected = new[]
            {
                "FolderLevel1.md",
                "SubFolder1/SubFolder1_FolderLevel2.md",
                "SubFolder2/SubFolder2_FolderLevel2.md",
                "SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob, excludeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesExcludingAFolder(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "**/*";
            string excludeGlob = "SubFolder2/SubSubFolder2";
            string[] expected = new[]
            {
                "FolderLevel1.txt",
                "FolderLevel1.md",
                "FolderLevel1_DifferentBaseName.txt",
                "SubFolder1/SubFolder1_FolderLevel2.txt",
                "SubFolder1/SubFolder1_FolderLevel2.md",
                "SubFolder2/SubFolder2_FolderLevel2.txt",
                "SubFolder2/SubFolder2_FolderLevel2.md",
                //"SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.txt",
                //"SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob, excludeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesExcludingAFoldersFiles(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "**/*";
            string excludeGlob = "SubFolder2/SubSubFolder2/**";
            string[] expected = new[]
            {
                "FolderLevel1.txt",
                "FolderLevel1.md",
                "FolderLevel1_DifferentBaseName.txt",
                "SubFolder1/SubFolder1_FolderLevel2.txt",
                "SubFolder1/SubFolder1_FolderLevel2.md",
                "SubFolder2/SubFolder2_FolderLevel2.txt",
                "SubFolder2/SubFolder2_FolderLevel2.md",
                //"SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.txt",
                //"SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob, excludeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesExcludingAFoldersFilesWithChildFolders(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "**/*";
            string excludeGlob = "SubFolder2/**";
            string[] expected = new[]
            {
                "FolderLevel1.txt",
                "FolderLevel1.md",
                "FolderLevel1_DifferentBaseName.txt",
                "SubFolder1/SubFolder1_FolderLevel2.txt",
                "SubFolder1/SubFolder1_FolderLevel2.md",
                //"SubFolder2/SubFolder2_FolderLevel2.txt",
                //"SubFolder2/SubFolder2_FolderLevel2.md",
                //"SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.txt",
                //"SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob, excludeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesUsingSubfolderAsBase(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "../**/*";
            string[] expected = new[]
            {
                "../FolderLevel1.txt",
                "../FolderLevel1.md",
                "../FolderLevel1_DifferentBaseName.txt",
                "../SubFolder1/SubFolder1_FolderLevel2.txt",
                "../SubFolder1/SubFolder1_FolderLevel2.md",
                "../SubFolder2/SubFolder2_FolderLevel2.txt",
                "../SubFolder2/SubFolder2_FolderLevel2.md",
                "../SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.txt",
                "../SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles/SubFolder2", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesUsingSubfolderAsBaseExcludingTxtFromBase(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "../**/*";
            string excludeGlob = "../**/*.txt";
            string[] expected = new[]
            {
                "../FolderLevel1.md",
                "../SubFolder1/SubFolder1_FolderLevel2.md",
                "../SubFolder2/SubFolder2_FolderLevel2.md",
                "../SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles/SubFolder2", includeGlob, excludeGlob);
        }

        [Theory]
        //[MemberData(nameof(AllGlobberFactoryMethods))]
        [MemberData(nameof(GlobberFactoryMethodsExcludingSystemGlobber))]
        public void FindAllFilesUsingSubfolderAsBaseNonRelativeExcludingTxtFromBase(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            // SystemGlobber does NOT handle this properly as it end up including ALL files

            string includeGlob = "../**/*";
            string excludeGlob = "**/*.txt";
            string[] expected = new[]
            {
                "../FolderLevel1.txt",
                "../FolderLevel1.md",
                "../FolderLevel1_DifferentBaseName.txt",
                "../SubFolder1/SubFolder1_FolderLevel2.txt",
                "../SubFolder1/SubFolder1_FolderLevel2.md",
                "../SubFolder2/SubFolder2_FolderLevel2.md",
                "../SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles/SubFolder2", includeGlob, excludeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesAlternate(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "**";
            string[] expected = new[]
            {
                "FolderLevel1.txt",
                "FolderLevel1.md",
                "FolderLevel1_DifferentBaseName.txt",
                "SubFolder1/SubFolder1_FolderLevel2.txt",
                "SubFolder1/SubFolder1_FolderLevel2.md",
                "SubFolder2/SubFolder2_FolderLevel2.txt",
                "SubFolder2/SubFolder2_FolderLevel2.md",
                "SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.txt",
                "SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }
        
        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesWithSameExtension(Func<IGlobberArgs, IGlobber> globberFactory)
        {
            string includeGlob = "**/*.txt";
            string[] expected = new[]
            {
                "FolderLevel1.txt",
                "FolderLevel1_DifferentBaseName.txt",
                "SubFolder1/SubFolder1_FolderLevel2.txt",
                "SubFolder2/SubFolder2_FolderLevel2.txt",
                "SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.txt",
            };

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Fact]
        public void CalculateFolderSegmentCountTests()
        {
            var paths = new (string Path, int ExpectedSegmentCount)[]
            {
                ("", 0),
                ("ZZZ", 1),
                ("Z/Z/Z", 3),
                ("Z*/*Z/*Z*", 3),
                ("Z/Z**/Z", -1),
                ("Z/**/Z", -1),
                ("**", -1),
                ("Z/./Z", 2),
                ("Z/../Z", 1),
                ("Z/*/Z", 3),
                ("Z/**/Z", -1),
                ("*A/**B/folderC/D*.txt", -1),
                ("*A/*B/folderC/D*.txt", 4),
                ("../..", -1),
            };

            foreach ((string Path, int ExpectedSegmentCount) x in paths)
            {
                int actual = ImprovedGlobber.CalculateFolderSegmentCount(x.Path);
                int expected = x.ExpectedSegmentCount;
                expected.Should().Be(actual);
            }
        }

        private static void ExecuteGlobAndValidate(Func<IGlobberArgs, IGlobber> globberFactoryMethod,
            IEnumerable<string> expected, string basePath, string includeGlob, string? excludeGlob = null)
        {
            ImmutableList<string> results = ExecuteGlob(globberFactoryMethod, basePath, includeGlob, excludeGlob);
            results.Should().BeEquivalentTo(expected);
        }

        private static ImmutableList<string> ExecuteGlob(Func<IGlobberArgs, IGlobber> globberFactoryMethod,
            string basePath, string includeGlob, string? excludeGlob = null, Func<GlobberTestArgs, GlobberTestArgs>? updateArgsFunc = null)
        {
            var args = new GlobberTestArgs()
            {
                BasePaths = new[] { basePath },
                IncludeGlobPaths = new[] { includeGlob },
                ExcludeGlobPaths = excludeGlob == null ? new string[] { } : new[] { excludeGlob },
            };

            if (updateArgsFunc != null)
                args = updateArgsFunc(args);

            IGlobber globber = globberFactoryMethod(args);

            return globber.Execute().ToImmutableList();
        }
    }
}