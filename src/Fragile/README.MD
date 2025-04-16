## Fragile

Fragile is a comprehensive archiving library for .NET, featuring file compression, encryption, error correction, and integrity verification. It provides advanced capabilities for creating and managing secure, reliable archives in .NET applications.

## Features

- **Split Archives**: Split large archives into manageable parts
- **Strong Encryption**: AES-256, ChaCha20, and Twofish encryption
- **Parallel Processing**: Optimized performance through parallel operations
- **Error Correction**: Reed-Solomon based error correction for file recovery
- **Metadata Support**: Store and retrieve custom metadata for archives and files
- **Integrity Verification**: CRC32 and other hashing algorithms for data integrity
- **Multiple Compression Algorithms**: Store, Brotli, Deflate, LZMA, LZ4, ZStd, and BZip2
- **Multi-Platform Support**: Works on .NET Framework 4.8/4.8.1, .NET 6-10 and .NET Standard 2.0/2.1

## Installation

```
dotnet add package Fragile
```

Or through the NuGet Package Manager:

```
Install-Package Fragile
```

## Basic Usage

### Creating an Archive

```csharp
using Fragile.Core;
using Fragile.Models;

// Create a new archive
using (FragileArchive archive = new FragileArchive("example.frgl", FragileArchiveMode.Create))
{
    // Add files and directories
    archive.AddFile("document.docx");
    archive.AddDirectory("images", recursive: true);
    
    // Save the archive
    archive.Save();
}
```

### Extracting an Archive

```csharp
// Open an existing archive
using (FragileArchive archive = new FragileArchive("example.frgl", FragileArchiveMode.Read))
{
    // Extract all files
    archive.ExtractAll("output_directory");
    
    // Or extract specific files
    archive.Extract("document.docx", "output_directory/document.docx");
}
```

## Advanced Features

### Compression Options

```csharp
var options = new FragileOptions
{
    CompressionLevel = CompressionLevel.Ultra,
    CompressionAlgorithm = CompressionAlgorithm.Deflate
};

using (FragileArchive archive = new FragileArchive("compressed.frgl", FragileArchiveMode.Create, options))
{
    // Add and save files with high compression
    archive.AddFile("large_file.bin");
    archive.Save();
}
```

### Encryption

```csharp
var options = new FragileOptions
{
    EnableEncryption = true,
    Password = "your-secure-password",
    EncryptionMethod = EncryptionMethod.AES256
};

using (FragileArchive archive = new FragileArchive("secure.frgl", FragileArchiveMode.Create, options))
{
    archive.AddFile("confidential.pdf");
    archive.Save();
}
```

### Error Correction

```csharp
var options = new FragileOptions
{
    EnableErrorCorrection = true,
    ErrorCorrectionLevel = 10 // Percentage of data redundancy
};

using (FragileArchive archive = new FragileArchive("recoverable.frgl", FragileArchiveMode.Create, options))
{
    archive.AddFile("important.db");
    archive.Save();
}
```

### Splitting Large Archives

```csharp
var options = new FragileOptions
{
    SplitSize = 100 * 1024 * 1024 // 100 MB parts
};

using (FragileArchive archive = new FragileArchive("large.frgl", FragileArchiveMode.Create, options))
{
    archive.AddDirectory("huge_directory", recursive: true);
    archive.Save();
    
    // Split the archive into parts
    FragileArchivePartCollection parts = await archive.SplitAsync("output");
    Console.WriteLine($"Archive split into {parts.Count} parts");
}
```

### Working with Metadata

```csharp
using (FragileArchive archive = new FragileArchive("metadata.frgl", FragileArchiveMode.Create))
{
    // Set archive-level metadata
    archive.Metadata.Author = "Your Name";
    archive.Metadata.CreationDate = DateTime.UtcNow;
    archive.Metadata.Description = "Project Backup";
    
    // Add files with metadata
    var entry = archive.AddFile("project.zip");
    
    // Set entry-specific metadata
    var entryMetadata = new EntryMetadata
    {
        Title = "Project Source Code",
        Keywords = new[] { "source", "backup", "project" },
        CustomProperties = new Dictionary<string, string>
        {
            { "Version", "1.0.0" },
            { "Department", "Development" }
        }
    };
    
    archive.SetEntryMetadata(entry.Path, entryMetadata);
    archive.Save();
}
```

## Async API Support

```csharp
// Create archive asynchronously
FragileArchive archive = await FragileArchive.CreateAsync("async.frgl");

// Add files
await archive.AddFileAsync("document.docx");

// Extract files asynchronously
await archive.ExtractAllAsync("output_directory");

// Save asynchronously
await archive.SaveAsync();
```

## Split Archive Creation

Fragile has the ability to automatically split large files into multiple parts when archiving. You can do this in two ways:

### 1. Splitting an existing archive:

```csharp
// Split an existing archive into 100 MB parts
FragileArchivePartCollection parts = await FragileUtility.SplitArchiveAsync("C:/Data/large-archive.frgl");

// List the parts
foreach (var part in parts)
{
    Console.WriteLine($"Part {part.PartIndex}/{part.TotalParts}: {part.Path} ({part.Size} bytes)");
}
```

### 2. Creating a directly split archive:

```csharp
// Split the archive directly during creation (each part will be 50 MB)
FragileOptions options = new FragileOptions
{
    CompressionLevel = CompressionLevel.Fastest,
    SplitSize = 50 * 1024 * 1024, // 50 MB parts
    CompressionAlgorithm = CompressionAlgorithm.Deflate
};

// Archive a file or folder and directly split it into parts
FragileArchivePartCollection parts = await FragileUtility.CreateSplitArchiveAsync(
    "C:/Data/LargeFolder", 
    "C:/Backup/archive.frgl", 
    true,
    options);

Console.WriteLine($"Archive split into {parts.Count} parts.");
```

### Combining Parts:

```csharp
// Combine all parts by specifying the first part
await FragileUtility.CombinePartsAsync(
    "C:/Backup/archive.part001.frgl", 
    "C:/Restore/archive.frgl");
    
// For password-protected parts:
FragileOptions options = new FragileOptions 
{
    Password = "my-secure-password"
};

await FragileUtility.CombinePartsAsync(
    "C:/Backup/archive.part001.frgl", 
    "C:/Restore/archive.frgl",
    options);
```

## License

This project is licensed under the terms of the LICENSE file included in the repository.

## Documentation

For more information, visit the [project website](https://fragile.soferity.com) or check the [GitHub repository](https://github.com/Taiizor/Fragile). 