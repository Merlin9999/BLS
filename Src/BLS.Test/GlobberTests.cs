using System.Collections.Immutable;
using BLS.Globbers;
using FluentAssertions;

namespace BLS.Test
{
    public class GlobberTests
    {
        public static TheoryData<Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>>> AllGlobberFactoryMethods =>
            new([
                (IGlobberArgs args) => new SystemFileGlobber(args),
                (IGlobberArgs args) => new FileGlobber(args)
            ]);

        public static TheoryData<Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>>> GlobberFactoryMethodsExcludingSystemGlobber =>
            new([
                (IGlobberArgs args) => new FileGlobber(args)
            ]);

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindSpecificFileNameWhereExists(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "FolderLevel1.txt";
            string[] expected = [includeGlob];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindSpecificFileNameWhereExistsUsingSubfolderAsBase(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "../FolderLevel1.txt";
            string[] expected = [includeGlob];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles/SubFolder1", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindSpecificFileNameWhereDoesNotExist(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "DoesNotExist.txt";
            string[] expected = [];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesInBaseFolder(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "*";
            string[] expected =
            [
                "FolderLevel1.txt",
                "FolderLevel1.md",
                "FolderLevel1_DifferentBaseName.txt"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesInBaseFolderWithSameBaseName(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "FolderLevel1.*";
            string[] expected =
            [
                "FolderLevel1.txt",
                "FolderLevel1.md"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesInBaseFolderWithSameExtension(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "*.txt";
            string[] expected =
            [
                "FolderLevel1.txt",
                "FolderLevel1_DifferentBaseName.txt"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Fact]
        public void FindAllFilesInTheWindowsFolderWithSameExtension()
        {
            string includeGlob = "*.exe";
            
            ImmutableList<string> systemGlobResults = ExecuteGlob(args => new SystemFileGlobber(args), @"C:\Windows", includeGlob);
            ImmutableList<string> improvedGlobResults = ExecuteGlob(args => new FileGlobber(args), @"C:\Windows", includeGlob);

            systemGlobResults.Should().BeEquivalentTo(improvedGlobResults);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFiles(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "**/*";
            string[] expected =
            [
                "FolderLevel1.txt",
                "FolderLevel1.md",
                "FolderLevel1_DifferentBaseName.txt",
                "SubFolder1/SubFolder1_FolderLevel2.txt",
                "SubFolder1/SubFolder1_FolderLevel2.md",
                "SubFolder2/SubFolder2_FolderLevel2.txt",
                "SubFolder2/SubFolder2_FolderLevel2.md",
                "SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.txt",
                "SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesExcludingTxt(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "**/*";
            string excludeGlob = "**/*.txt";
            string[] expected =
            [
                "FolderLevel1.md",
                "SubFolder1/SubFolder1_FolderLevel2.md",
                "SubFolder2/SubFolder2_FolderLevel2.md",
                "SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob, excludeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesExcludingAFolder(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "**/*";
            string excludeGlob = "SubFolder2/SubSubFolder2";
            string[] expected =
            [
                "FolderLevel1.txt",
                "FolderLevel1.md",
                "FolderLevel1_DifferentBaseName.txt",
                "SubFolder1/SubFolder1_FolderLevel2.txt",
                "SubFolder1/SubFolder1_FolderLevel2.md",
                "SubFolder2/SubFolder2_FolderLevel2.txt",
                "SubFolder2/SubFolder2_FolderLevel2.md"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob, excludeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesExcludingAFoldersFiles(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "**/*";
            string excludeGlob = "SubFolder2/SubSubFolder2/**";
            string[] expected =
            [
                "FolderLevel1.txt",
                "FolderLevel1.md",
                "FolderLevel1_DifferentBaseName.txt",
                "SubFolder1/SubFolder1_FolderLevel2.txt",
                "SubFolder1/SubFolder1_FolderLevel2.md",
                "SubFolder2/SubFolder2_FolderLevel2.txt",
                "SubFolder2/SubFolder2_FolderLevel2.md"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob, excludeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesExcludingAFoldersFilesWithChildFolders(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "**/*";
            string excludeGlob = "SubFolder2/**";
            string[] expected =
            [
                "FolderLevel1.txt",
                "FolderLevel1.md",
                "FolderLevel1_DifferentBaseName.txt",
                "SubFolder1/SubFolder1_FolderLevel2.txt",
                "SubFolder1/SubFolder1_FolderLevel2.md"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob, excludeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesUsingSubfolderAsBase(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "../**/*";
            string[] expected =
            [
                "../FolderLevel1.txt",
                "../FolderLevel1.md",
                "../FolderLevel1_DifferentBaseName.txt",
                "../SubFolder1/SubFolder1_FolderLevel2.txt",
                "../SubFolder1/SubFolder1_FolderLevel2.md",
                "../SubFolder2/SubFolder2_FolderLevel2.txt",
                "../SubFolder2/SubFolder2_FolderLevel2.md",
                "../SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.txt",
                "../SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles/SubFolder2", includeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesUsingSubfolderAsBaseExcludingTxtFromBase(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "../**/*";
            string excludeGlob = "../**/*.txt";
            string[] expected =
            [
                "../FolderLevel1.md",
                "../SubFolder1/SubFolder1_FolderLevel2.md",
                "../SubFolder2/SubFolder2_FolderLevel2.md",
                "../SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles/SubFolder2", includeGlob, excludeGlob);
        }

        [Theory]
        //[MemberData(nameof(AllGlobberFactoryMethods))]
        [MemberData(nameof(GlobberFactoryMethodsExcludingSystemGlobber))]
        public void FindAllFilesUsingSubfolderAsBaseNonRelativeExcludingTxtFromBase(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            // SystemFileGlobber does NOT handle this properly as it end up including ALL files

            string includeGlob = "../**/*";
            string excludeGlob = "**/*.txt";
            string[] expected =
            [
                "../FolderLevel1.txt",
                "../FolderLevel1.md",
                "../FolderLevel1_DifferentBaseName.txt",
                "../SubFolder1/SubFolder1_FolderLevel2.txt",
                "../SubFolder1/SubFolder1_FolderLevel2.md",
                "../SubFolder2/SubFolder2_FolderLevel2.md",
                "../SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles/SubFolder2", includeGlob, excludeGlob);
        }

        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesAlternate(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "**";
            string[] expected =
            [
                "FolderLevel1.txt",
                "FolderLevel1.md",
                "FolderLevel1_DifferentBaseName.txt",
                "SubFolder1/SubFolder1_FolderLevel2.txt",
                "SubFolder1/SubFolder1_FolderLevel2.md",
                "SubFolder2/SubFolder2_FolderLevel2.txt",
                "SubFolder2/SubFolder2_FolderLevel2.md",
                "SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.txt",
                "SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.md"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }
        
        [Theory]
        [MemberData(nameof(AllGlobberFactoryMethods))]
        public void FindAllFilesWithSameExtension(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactory)
        {
            string includeGlob = "**/*.txt";
            string[] expected =
            [
                "FolderLevel1.txt",
                "FolderLevel1_DifferentBaseName.txt",
                "SubFolder1/SubFolder1_FolderLevel2.txt",
                "SubFolder2/SubFolder2_FolderLevel2.txt",
                "SubFolder2/SubSubFolder2/SubSubFolder2_FolderLevel3.txt"
            ];

            ExecuteGlobAndValidate(globberFactory, expected, "GlobTestFiles", includeGlob);
        }

        private static void ExecuteGlobAndValidate(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactoryMethod,
            IEnumerable<string> expected, string basePath, string includeGlob, string? excludeGlob = null)
        {
            ImmutableList<string> results = ExecuteGlob(globberFactoryMethod, basePath, includeGlob, excludeGlob);
            results.Should().BeEquivalentTo(expected);
        }

        private static ImmutableList<string> ExecuteGlob(Func<IGlobberArgs, IGlobber<FilePathInfo, FileInfo>> globberFactoryMethod,
            string basePath, string includeGlob, string? excludeGlob = null, Func<GlobberTestArgs, GlobberTestArgs>? updateArgsFunc = null)
        {
            var args = new GlobberTestArgs()
            {
                BasePaths = new[] { basePath },
                IncludeGlobPaths = new[] { includeGlob },
                ExcludeGlobPaths = excludeGlob == null ? [] : new[] { excludeGlob },
            };

            if (updateArgsFunc != null)
                args = updateArgsFunc(args);

            IGlobber<FilePathInfo, FileInfo> globber = globberFactoryMethod(args);

            return globber.Execute().Select(p => AbstractGlobber.ToAlternatePathSegmentSeparators(p.EntryPath)).ToImmutableList();
        }
    }
}