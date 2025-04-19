# Fragile Library Features

Fragile is a custom file archiving library for .NET that provides advanced features beyond standard archiving capabilities. The library is designed to create robust and secure archives with support for various compression, encryption and data integrity mechanisms.

## Core Features

### Archive Management

- **Cancellation Support**: Cancel long-running operations via CancellationToken
- **Progress Reporting**: Built-in progress tracking for long-running operations
- **Archive Verification**: Signature and version verification for archive integrity
- **Synchronous & Asynchronous API**: Full support for both sync and async operations
- **Stream-based Processing**: Efficient handling of files with stream-based architecture
- **Basic Operations**: Create, open, extract and update archive files with `.frgl` extension
- **File & Directory Support**: Add individual files or entire directory structures to archives

### Compression

- **Compression Algorithms**:
  - GZip
  - Store (no compression)
  - ZLib (only available on .NET 7.0 or greater)
  - Deflate (standard compression compatible with ZIP)
  - Brotli (only available on .NET Standard 2.1, .NET 6.0 or greater)

- **Compression Options**:
  - Configurable thread count for parallel processing
  - Parallel compression/decompression using multiple threads
  - Solid compression mode for better compression of similar files
  - Adjustable compression levels (Fastest, Fast, Normal, High, Ultra)

### Encryption

- **Encryption Algorithms**:
  - AES-128 encryption
  - AES-256 encryption
  - Twofish encryption
  - ChaCha20-Poly1305 encryption

- **Security Features**:
  - Password-based encryption
  - Per-file encryption settings
  - Secure initialization vectors (IV)
  - Salt-based key derivation with PBKDF2

### Data Integrity & Error Correction

- **Checksum Verification**:
  - MD5 hash
  - SHA-1 hash
  - SHA-256 hash
  - SHA-384 hash
  - SHA-512 hash
  - CRC32 checksums

- **Error Correction**:
  - Reed-Solomon error correction codes
  - Per-file or global error correction settings
  - Automatic error detection and repair capabilities
  - Galois Field implementation for algebraic error correction
  - Configurable error correction level (percentage of archive size)

### Metadata Support

- **Archive-level Metadata**:
  - Custom tags
  - Version tracking
  - Author information
  - Title and description
  - Application-specific data
  - JSON serialization format
  - Creation and modification timestamps

- **File-level Metadata**:
  - File attributes
  - Last access time
  - MIME type detection
  - File owner and group
  - Original creation time
  - Custom tags and comments
  - Custom properties for application-specific needs

## Advanced Features

### Archive Splitting

- Concurrent processing for large archives
- Parallel and sequential archive splitting modes
- Automatic handling of multi-part archives during extraction
- Split large archives into multiple parts with configurable size limits

### Parallel Processing

- Parallel file processing for large archives
- Multi-threaded compression and decompression
- Configurable thread count for optimal performance
- Auto-detection of when to use parallel processing based on file size

### Extensibility

- Support for custom metadata extensions
- Easy addition of new algorithms and methods
- Pluggable component system for different implementations
- Provider-based architecture for compression, encryption and verification

### Native AOT Support

- Enhanced cross-platform deployment capabilities
- Optimized runtime performance with native CPU instructions
- Full support for Ahead-of-Time compilation on .NET 7.0 and later
- Significantly improved startup time with pre-compiled native code
- Optimized for server-side applications and containerized deployments
- Reduced memory footprint through trimming and ahead-of-time compilation

## Integration

- Modern C# language features
- Minimal external dependencies
- System.Text.Json integration for metadata serialization
- Compatible with .NET Framework 4.8/4.8.1, .NET 6-10 and .NET Standard 2.0/2.1
- **Native AOT Support**: Ahead-of-Time compilation support for improved performance, startup time and memory usage on .NET 7.0 and later