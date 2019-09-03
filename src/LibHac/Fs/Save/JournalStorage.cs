﻿using System;
using System.Collections;
using System.IO;

namespace LibHac.Fs.Save
{
    public class JournalStorage : StorageBase
    {
        private IStorage BaseStorage { get; }
        private IStorage HeaderStorage { get; }
        public JournalMap Map { get; }

        public JournalHeader Header { get; }

        public int BlockSize { get; }

        private long _length;

        public JournalStorage(IStorage baseStorage, IStorage header, JournalMapParams mapInfo, bool leaveOpen)
        {
            BaseStorage = baseStorage;
            HeaderStorage = header;
            Header = new JournalHeader(HeaderStorage);

            IStorage mapHeader = header.Slice(0x20, 0x10);
            Map = new JournalMap(mapHeader, mapInfo);

            BlockSize = (int)Header.BlockSize;
            _length = Header.TotalSize - Header.JournalSize;

            if (!leaveOpen) ToDispose.Add(baseStorage);
        }

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);
                int blockPos = (int)(inPos % BlockSize);

                long physicalOffset = Map.GetPhysicalBlock(blockNum) * BlockSize + blockPos;

                int bytesToRead = Math.Min(remaining, BlockSize - blockPos);

                Result rc = BaseStorage.Read(physicalOffset, destination.Slice(outPos, bytesToRead));
                if (rc.IsFailure()) return rc;

                outPos += bytesToRead;
                inPos += bytesToRead;
                remaining -= bytesToRead;
            }

            return Result.Success;
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = source.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);
                int blockPos = (int)(inPos % BlockSize);

                long physicalOffset = Map.GetPhysicalBlock(blockNum) * BlockSize + blockPos;

                int bytesToWrite = Math.Min(remaining, BlockSize - blockPos);

                Result rc = BaseStorage.Write(physicalOffset, source.Slice(outPos, bytesToWrite));
                if (rc.IsFailure()) return rc;

                outPos += bytesToWrite;
                inPos += bytesToWrite;
                remaining -= bytesToWrite;
            }

            return Result.Success;
        }

        public override Result Flush()
        {
            return BaseStorage.Flush();
        }

        public override Result GetSize(out long size)
        {
            size = _length;
            return Result.Success;
        }

        public IStorage GetBaseStorage() => BaseStorage.AsReadOnly();
        public IStorage GetHeaderStorage() => HeaderStorage.AsReadOnly();

        public void FsTrim()
        {
            // todo replace with a bitmap reader class when added
            BitArray bitmap = new DuplexBitmap(Map.GetFreeBlocksStorage(),
                Map.Header.JournalBlockCount + Map.Header.MainDataBlockCount).Bitmap;

            for (int i = 0; i < bitmap.Length; i++)
            {
                if (!bitmap[i]) continue;

                BaseStorage.Fill(SaveDataFileSystem.TrimFillValue, i * BlockSize, BlockSize);
            }

            Map.FsTrim();
        }
    }

    public class JournalHeader
    {
        public string Magic { get; }
        public uint Version { get; }
        public long TotalSize { get; }
        public long JournalSize { get; }
        public long BlockSize { get; }

        public JournalHeader(IStorage storage)
        {
            var reader = new BinaryReader(storage.AsStream());

            Magic = reader.ReadAscii(4);
            Version = reader.ReadUInt32();
            TotalSize = reader.ReadInt64();
            JournalSize = reader.ReadInt64();
            BlockSize = reader.ReadInt64();
        }
    }

    public class JournalMapEntry
    {
        public int PhysicalIndex { get; set; }
        public int VirtualIndex { get; set; }
    }
}
