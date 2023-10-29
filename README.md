# Bi-Directional gRPC Stream Cache Server in C#

This repository provides a comprehensive implementation of a gRPC client-server communication system using C#, enabling the execution and broadcasting of commands from the server to connected clients.

## Features

- **Client Management**: Efficiently manage connected clients, keeping track of their state and availability.
- **Command Execution**: Execute commands on clients and handle their responses.
- **Broadcasting**: Send a command to all connected clients and process their responses.
- **Robust Error Handling**: Handle disconnections, timeouts, and errors gracefully.
- **Concurrency Control**: Ensure thread-safe interactions using semaphores and locks.
- **Logging**: Utilize logging to track the flow of commands and responses, aiding in debugging and performance monitoring.
- **Extensibility**: Easily extend the server and client implementations to add more command types and functionalities.

## Getting Started

### Prerequisites

Ensure you have the following installed:

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Visual Studio](https://visualstudio.microsoft.com/) or any preferred IDE with C# support

### Installation

Clone the repository:

```sh
git clone https://github.com/goatreview/SimplegRPCCache.git
```

Navigate to the project directory:

```sh
cd SimplegRPCCache
```

Restore the .NET packages:

```sh
dotnet restore
```

Running with docker:

```sh
docker compose build
docker compose up
```

### Usage

1. Start the gRPC server application.
2. Start one or more gRPC client applications.
3. Utilize the implemented features like command execution and broadcasting.

## Documentation

Refer to the inline XML documentation in the codebase for detailed information on methods, properties, and usage.

## Contributing

Contributions are welcome! Please follow the [Contributing Guide](CONTRIBUTING.md) for more information.

## License

Distributed under the GNU General Public License v3.0. See [GNU General Public License v3.0](https://github.com/goatreview/SimplegRPCCache/blob/main/LICENSE) for more information.

## Contact

Goat Review - [Goat Review Contact](https://goatreview.com/contact)

Goat Review related posts - [Bi-Directional gRPC Stream Cache Server in C#](https://goatreview.com/bi-directional-grpc-stream-cache-server-csharp)

Project Link: [https://github.com/goatreview/SimplegRPCCache](https://github.com/goatreview/SimplegRPCCache)

## Acknowledgements

Overview for gRPC on .NET https://learn.microsoft.com/en-us/aspnet/core/grpc/?view=aspnetcore-8.0

---

This README provides a concise and straightforward guide to understanding, installing, and using the gRPC client-server communication system, making it easy for contributors and users to get started.