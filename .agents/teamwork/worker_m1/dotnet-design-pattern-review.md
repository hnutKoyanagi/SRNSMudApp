# .NET/C# Design Pattern Review

Review C#/.NET code for design pattern implementation and best practices:

## Required Design Patterns
- **Command Pattern**: Generic base classes (`CommandHandler<TOptions>`), `ICommandHandler<TOptions>` interface, `CommandHandlerOptions` inheritance
- **Factory Pattern**: Complex object creation service provider integration
- **Dependency Injection**: Primary constructor syntax, `ArgumentNullException` null checks, interface abstractions, proper service lifetimes
- **Repository Pattern**: Async data access interfaces provider abstractions for connections
- **Provider Pattern**: External service abstractions, clear contracts, configuration handling
- **Resource Pattern**: ResourceManager for localized messages

## Review Checklist
- **Design Patterns**: Identify patterns used. Correctly implemented?
- **Architecture**: Proper separation of concerns, modular and readable
- **.NET Best Practices**: Primary constructors, async/await with Task returns, structured logging, strongly-typed configuration
- **SOLID Principles**: SRP, OCP, LSP, ISP, DIP compliance
- **Performance**: Proper async/await, resource disposal, thread safety
- **Security**: Input validation, secure credential handling, authorization, safe exception handling
- **Documentation**: XML docs for public APIs, Japanese comments for design intent
