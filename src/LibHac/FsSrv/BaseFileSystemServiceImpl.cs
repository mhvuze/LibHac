﻿using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Creators;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Sf;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;

namespace LibHac.FsSrv
{
    public class BaseFileSystemServiceImpl
    {
        private Configuration _config;

        public delegate Result BisWiperCreator(out IWiper wiper, NativeHandle transferMemoryHandle,
            ulong transferMemorySize);

        public BaseFileSystemServiceImpl(in Configuration configuration)
        {
            _config = configuration;
        }

        public struct Configuration
        {
            public IBuiltInStorageFileSystemCreator BisFileSystemCreator;
            public IGameCardFileSystemCreator GameCardFileSystemCreator;
            public ISdCardProxyFileSystemCreator SdCardFileSystemCreator;
            // CurrentTimeFunction
            // FatFileSystemCacheManager
            // AlbumDirectoryFileSystemManager
            public BisWiperCreator BisWiperCreator;

            // Note: The program registry service is global as of FS 10.0.0
            public ProgramRegistryImpl ProgramRegistry;
        }

        public Result OpenBaseFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, int fileSystemId)
        {
            throw new NotImplementedException();
        }

        public Result OpenBisFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, U8Span rootPath,
            BisPartitionId partitionId)
        {
            return OpenBisFileSystem(out fileSystem, rootPath, partitionId, false);
        }

        public Result OpenBisFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, U8Span rootPath,
            BisPartitionId partitionId, bool caseSensitive)
        {
            fileSystem = default;

            Result rc = _config.BisFileSystemCreator.Create(out IFileSystem fs, rootPath.ToString(), partitionId);
            if (rc.IsFailure()) return rc;

            fileSystem = new ReferenceCountedDisposable<IFileSystem>(fs);
            return Result.Success;
        }

        public Result CreatePaddingFile(long size)
        {
            throw new NotImplementedException();
        }

        public Result DeleteAllPaddingFiles()
        {
            throw new NotImplementedException();
        }

        public Result OpenGameCardFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, GameCardHandle handle,
            GameCardPartition partitionId)
        {
            Result rc;
            int tries = 0;

            do
            {
                rc = _config.GameCardFileSystemCreator.Create(out fileSystem, handle, partitionId);

                if (!ResultFs.DataCorrupted.Includes(rc))
                    break;

                tries++;
            } while (tries < 2);

            return rc;
        }

        public Result OpenSdCardProxyFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem)
        {
            return OpenSdCardProxyFileSystem(out fileSystem, false);
        }

        public Result OpenSdCardProxyFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, bool openCaseSensitive)
        {
            fileSystem = default;

            // Todo: Shared
            Result rc = _config.SdCardFileSystemCreator.Create(out IFileSystem fs, openCaseSensitive);
            if (rc.IsFailure()) return rc;

            fileSystem = new ReferenceCountedDisposable<IFileSystem>(fs);
            return Result.Success;
        }

        public Result FormatSdCardProxyFileSystem()
        {
            return _config.SdCardFileSystemCreator.Format();
        }

        public Result FormatSdCardDryRun()
        {
            throw new NotImplementedException();
        }

        public bool IsExFatSupported()
        {
            // Returning false should probably be fine
            return false;
        }

        public Result OpenBisWiper(out IWiper wiper, NativeHandle transferMemoryHandle, ulong transferMemorySize)
        {
            return _config.BisWiperCreator(out wiper, transferMemoryHandle, transferMemorySize);
        }

        internal Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            return _config.ProgramRegistry.GetProgramInfo(out programInfo, processId);
        }
    }
}
