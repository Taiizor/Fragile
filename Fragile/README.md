# Fragile Library

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/fragile-team/fragile/actions)
[![NuGet Version](https://img.shields.io/nuget/v/Fragile.svg)](https://www.nuget.org/packages/Fragile/)
[![License](https://img.shields.io/github/license/fragile-team/fragile)](https://github.com/fragile-team/fragile/blob/main/LICENSE)

Fragile is a custom file archiving library for .NET that provides advanced features beyond standard archiving capabilities. The library is designed to create robust and secure archives with support for various compression, encryption, and data integrity mechanisms.

## Features

### Archive Management
- **Basic Operations**: Create, open, extract, and update archive files with `.frgl` extension
- **File & Directory Support**: Add individual files or entire directory structures to archives
- **Synchronous & Asynchronous API**: Full support for both sync and async operations
- **Stream-based Processing**: Efficient handling of files with stream-based architecture
- **Progress Reporting**: Built-in progress tracking for long-running operations
- **Archive Verification**: Signature and version verification for archive integrity
- **Cancellation Support**: Cancel long-running operations via CancellationToken

### Compression
- **Algorithms**: Store (no compression), Deflate (ZIP compatible)
- **Options**: Adjustable compression levels, solid mode, parallel processing

### Encryption
- **Algorithms**: AES-128, AES-256
- **Security**: Password-based encryption, salt-based key derivation with PBKDF2, per-file settings

### Data Integrity & Error Correction
- **Checksums**: CRC32, MD5, SHA-1, SHA-256, SHA-512
- **Error Correction**: Reed-Solomon codes, configurable error correction levels, automatic repair

### Metadata Support
- **Archive-level**: Timestamps, author, title, description, version, tags, application data
- **File-level**: Timestamps, attributes, owner, MIME type, tags, comments, custom properties

### Advanced Features
- **Archive Splitting**: Split large archives into parts with configurable size limits
- **Parallel Processing**: Multi-threaded compression/decompression with configurable thread count
- **Extensibility**: Provider-based architecture for custom algorithms and metadata extensions

## Installation

You can install Fragile via NuGet Package Manager:

```bash
Install-Package Fragile
```

Or using the .NET CLI:

```bash
dotnet add package Fragile
```

## Usage

```csharp
using Fragile.Archive;
using Fragile.Compression;
using Fragile.Encryption;
using System.Threading.Tasks;

async Task Example()
{
    var manager = new ArchiveManager();
    
    // Create a new archive with compression and encryption
    var compressionOptions = new CompressionOptions(CompressionAlgorithm.Deflate, CompressionLevel.Normal);
    var encryptionOptions = new EncryptionOptions(EncryptionAlgorithm.AES256, "MySecurePassword");
    
    // Additional configuration can be set here
    
    await manager.CreateArchiveAsync("myArchive.frgl");
    await manager.UpdateArchiveAsync("myArchive.frgl", "path/to/files");
    await manager.ExtractArchiveAsync("myArchive.frgl", "path/to/extract");
}
```

## Documentation

Full documentation is available at [our GitHub repository](https://github.com/fragile-team/fragile/wiki).

## Contributing

We welcome contributions! Please see our [Contributing Guide](https://github.com/fragile-team/fragile/blob/main/CONTRIBUTING.md) for more details.

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/fragile-team/fragile/blob/main/LICENSE) file for details.

## Contact

For questions or support, please open an issue on our [GitHub repository](https://github.com/fragile-team/fragile/issues). 