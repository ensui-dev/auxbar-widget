# Contributing to Auxbar Desktop Client

Thank you for your interest in contributing to Auxbar! This document provides guidelines and information for contributors.

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/yourusername/auxbar-client.git`
3. Create a branch: `git checkout -b feature/your-feature-name`

## Development Environment

### Prerequisites

- Windows 10/11 (required for Windows Media Session API)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or VS Code with C# extension

### Building

```bash
dotnet restore
dotnet build
```

### Running

```bash
dotnet run --project AuxbarClient
```

## Code Style

- Follow standard C# naming conventions
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and reasonably sized

## Pull Request Process

1. Ensure your code builds without warnings
2. Test your changes thoroughly
3. Update documentation if needed
4. Create a clear PR description explaining your changes
5. Link any related issues

## Reporting Issues

When reporting bugs, please include:

- Windows version
- .NET version (`dotnet --version`)
- Steps to reproduce
- Expected vs actual behavior
- Relevant logs or error messages

## Feature Requests

Feature requests are welcome! Please open an issue describing:

- The problem you're trying to solve
- Your proposed solution
- Any alternatives you've considered

## Code of Conduct

- Be respectful and inclusive
- Focus on constructive feedback
- Help others learn and grow

## Questions?

Feel free to open an issue for any questions about contributing.
