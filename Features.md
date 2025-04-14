# Fragile Library Features

Fragile is a custom file archiving library for .NET that provides advanced features beyond standard archiving capabilities. The library is designed to create robust and secure archives with support for various compression, encryption, and data integrity mechanisms.

## Core Features

### Archive Management

- **Basic Operations**: Create, open, extract, and update archive files with `.frgl` extension
- **File & Directory Support**: Add individual files or entire directory structures to archives
- **Synchronous & Asynchronous API**: Full support for both sync and async operations
- **Stream-based Processing**: Efficient handling of files with stream-based architecture
- **Progress Reporting**: Built-in progress tracking for long-running operations
- **Archive Verification**: Signature and version verification for archive integrity
- **Cancellation Support**: Cancel long-running operations via CancellationToken

### Compression

- **Compression Algorithms**:
  - Store (no compression)
  - Deflate (standard compression compatible with ZIP)
  
  > **Note**: LZMA, BZip2, ZStd, and LZ4 are defined in the enum but not currently implemented.

- **Compression Options**:
  - Adjustable compression levels (Fastest, Fast, Normal, High, Ultra)
  - Solid compression mode for better compression of similar files
  - Parallel compression/decompression using multiple threads
  - Configurable thread count for parallel processing

### Encryption

- **Encryption Algorithms**:
  - AES-128 encryption
  - AES-256 encryption
  - ChaCha20-Poly1305 encryption
  - Twofish encryption

- **Security Features**:
  - Password-based encryption
  - Salt-based key derivation with PBKDF2
  - Per-file encryption settings
  - Secure initialization vectors (IV)

### Data Integrity & Error Correction

- **Checksum Verification**:
  - CRC32 checksums
  - MD5 hash
  - SHA-1 hash
  - SHA-256 hash
  - SHA-512 hash

- **Error Correction**:
  - Reed-Solomon error correction codes
  - Configurable error correction level (percentage of archive size)
  - Automatic error detection and repair capabilities
  - Per-file or global error correction settings
  - Galois Field implementation for algebraic error correction

### Metadata Support

- **Archive-level Metadata**:
  - Creation and modification timestamps
  - Author information
  - Title and description
  - Version tracking
  - Custom tags
  - Application-specific data
  - JSON serialization format

- **File-level Metadata**:
  - Original creation time
  - Last access time
  - File attributes
  - File owner and group
  - MIME type detection
  - Custom tags and comments
  - Custom properties for application-specific needs

## Advanced Features

### Archive Splitting

- Split large archives into multiple parts with configurable size limits
- Automatic handling of multi-part archives during extraction
- Parallel and sequential archive splitting modes
- Concurrent processing for large archives

### Parallel Processing

- Multi-threaded compression and decompression
- Configurable thread count for optimal performance
- Parallel file processing for large archives
- Auto-detection of when to use parallel processing based on file size

### Extensibility

- Provider-based architecture for compression, encryption, and verification
- Easy addition of new algorithms and methods
- Support for custom metadata extensions
- Pluggable component system for different implementations

## Integration

- Compatible with .NET Standard 2.1
- Modern C# language features
- System.Text.Json integration for metadata serialization
- Minimal external dependencies