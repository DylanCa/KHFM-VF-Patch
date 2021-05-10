﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xe.BinaryMapper;

namespace KHFM_VF_Patch
{
    public class Asset
    {
        public class Header
        {
            [Data] public int DecompressedLength { get; set; }
            [Data] public int RemasteredAssetCount { get; set; }
            [Data] public int CompressedLength { get; set; }
            [Data] public int Unknown0c { get; set; }
        }

        public class RemasteredEntry
        {
            [Data(Count = 0x20)] public string Name { get; set; }
            [Data] public int Offset { get; set; }
            [Data] public int Unknown24 { get; set; }
            [Data] public int DecompressedLength { get; set; }
            [Data] public int CompressedLength { get; set; }
        }

        private static readonly string ResourcePath = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "resources");
        public static string[] DddNames = File.ReadAllLines(Path.Combine(ResourcePath, "ddd.txt"));
        public static string[] MareNames = File.ReadAllLines(Path.Combine(ResourcePath, "mare.txt"));
        public static string[] SettingMenuNames = File.ReadAllLines(Path.Combine(ResourcePath, "settingmenu.txt"));
        public static string[] TheaterNames = File.ReadAllLines(Path.Combine(ResourcePath, "theater.txt"));
        public static string[] RecomNames = File.ReadAllLines(Path.Combine(ResourcePath, "recom.txt"));
        public static string[] BbsNames = File.ReadAllLines(Path.Combine(ResourcePath, "bbs.txt"));
        public static string[] Kh1AdditionalNames = File.ReadAllLines(Path.Combine(ResourcePath, "kh1pc.txt"));
        public static string[] Launcher28Names = File.ReadAllLines(Path.Combine(ResourcePath, "launcher28.txt"));

        private const int PassCount = 10;
        private readonly Stream _stream;
        private readonly Header _header;
        private readonly byte[] _key;
        private readonly long _baseOffset;
        private readonly long _dataOffset;
        private readonly Dictionary<string, RemasteredEntry> _entries;
        private readonly Hed.Entry _hedEntry;

        public string[] Assets { get; }
        public Header OriginalAssetHeader => _header;
        public Dictionary<string, RemasteredEntry> RemasteredAssetHeaders => _entries;

        public Asset(Stream stream, Hed.Entry hedEntry)
        {
            _hedEntry = hedEntry;
            _stream = stream;
            _baseOffset = stream.Position;

            var seed = stream.ReadBytes(0x10);
            _key = Encryption.GenerateKey(seed, PassCount);

            _header = BinaryMapping.ReadObject<Header>(new MemoryStream(seed));

            var entries = Enumerable
                .Range(0, _header.RemasteredAssetCount)
                .Select(_ => BinaryMapping.ReadObject<RemasteredEntry>(stream))
                .ToList();

            _entries = entries.ToDictionary(x => x.Name, x => x);
            _dataOffset = stream.Position;

            Assets = entries.Select(x => x.Name).ToArray();
        }

        public byte[] ReadRemasteredAsset(string assetName)
        {
            var header = _entries[assetName];
            var dataLength = header.CompressedLength >= 0 ? header.CompressedLength : header.DecompressedLength;

            if (dataLength % 16 != 0)
                dataLength += 16 - (dataLength % 16);

            var data = _stream.AlignPosition(0x10).ReadBytes(dataLength);

            for (var i = 0; i < Math.Min(dataLength, 0x100); i += 0x10)
                Encryption.DecryptChunk(_key, data, i, PassCount);

            if (header.CompressedLength >= 0)
            {
                using var compressedStream = new MemoryStream(data);
                using var deflate = new DeflateStream(compressedStream.SetPosition(2), CompressionMode.Decompress);

                var decompressedData = new byte[header.DecompressedLength];
                deflate.Read(decompressedData);

                return decompressedData;
            }

            return data;
        }

        public byte[] ReadData()
        {
            var dataLength = _header.CompressedLength >= 0 ? _header.CompressedLength : _header.DecompressedLength;
            var data = _stream.SetPosition(_dataOffset).ReadBytes(dataLength);

            if (_header.CompressedLength >= -1)
            {
                for (var i = 0; i < Math.Min(dataLength, 0x100); i += 0x10)
                    Encryption.DecryptChunk(_key, data, i, PassCount);
            }

            if (_header.CompressedLength >= 0)
            {
                using var compressedStream = new MemoryStream(data);
                using var deflate = new DeflateStream(compressedStream.SetPosition(2), CompressionMode.Decompress);

                var decompressedData = new byte[_header.DecompressedLength];
                deflate.Read(decompressedData);

                return decompressedData;
            }

            return data;
        }
    }
}