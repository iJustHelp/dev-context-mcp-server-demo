---
name: csharp-naming
description: >-
  Applies company C# naming conventions, member ordering, and constructor style.
  Use when generating or refactoring C# code, classes, methods, properties,
  fields, or async members.
---

# C# Naming Conventions

Follow standard Microsoft C# naming conventions. The rules below are normative:
when generating or refactoring C# code, apply them automatically.

## Casing Rules

| Element | Convention | Example |
| --- | --- | --- |
| Classes, records, structs, enums, methods, properties, events, public members | `PascalCase` | `CustomerService`, `GetCustomerById` |
| Local variables, method parameters | `camelCase` | `customerId`, `customerName` |
| Private fields | `_camelCase` | `_customerRepository` |
| Interfaces | `I` prefix + `PascalCase` | `ICustomerRepository` |
| Constants | `PascalCase` | `MaxRetryCount` |
| Asynchronous methods | `PascalCase` + `Async` suffix | `GetCustomerByIdAsync` |

## General Rules

* Use clear, descriptive names. Avoid unclear abbreviations.
* Add a summary comment above each class.
* Do not use Hungarian notation.
* Do not use underscores in public type, method, or property names.

## Examples

Classes:

```csharp
/// <summary>
/// Loads and caches NuGet package policy files, enforcing a single source folder per process.
/// </summary>
internal sealed class NuGetPackageOptionsLoader : INuGetPackageOptionsLoader
```

Interfaces:

```csharp
public interface ICustomerRepository
public interface IEmailSender
```

Methods (note the `Async` suffix on the asynchronous overload):

```csharp
public Customer GetCustomerById(int customerId)
public Task<Customer> GetCustomerByIdAsync(int customerId)
```

Properties:

```csharp
public string FirstName { get; set; }
public DateTime CreatedDate { get; set; }
```

Private fields:

```csharp
private readonly ICustomerRepository _customerRepository;
private readonly ILogger<CustomerService> _logger;
```

Local variables and parameters:

```csharp
var customerName = customer.FirstName;

public Customer GetCustomer(int customerId)
{
    ...
}
```

Constants:

```csharp
private const int MaxRetryCount = 3;
public const string DefaultStatus = "Active";
```

Enums:

```csharp
public enum OrderStatus
{
    Pending,
    Approved,
    Rejected
}
```

## Avoid

These violate the rules above (`snake_case` member, `PascalCase` field, missing `Async` suffix, `PascalCase` parameter):

```csharp
public class customer_service
public string first_name { get; set; }
private readonly ILogger Logger;
public Task<Customer> GetCustomer(int CustomerId)
```

## Object Creation and Constructor Arguments

* Do not use target-typed `new()`. Always write the explicit type name for readability and easier code review.
* If a constructor or method call has **more than 3 arguments**, use named arguments.
* Format long constructor calls across multiple lines, one argument per line.
* If a constructor needs many parameters, consider whether a configuration object, options class, builder, or request model would make the code cleaner.

### Examples

Avoid target-typed `new()`:

```csharp
var customer = new("John", "Smith", "john@email.com");
```

Use the explicit type name (3 arguments, so positional is fine):

```csharp
var customer = new Customer("John", "Smith", "john@email.com");
```

Avoid positional arguments when there are more than 3:

```csharp
var order = new Order(123, customerId, amount, tax);
```

Use named arguments, one per line, when there are more than 3:

```csharp
var order = new Order(
    orderId: 123,
    customerId: customerId,
    amount: amount,
    tax: tax,
    discount: discount,
    status: status,
    createdDate: createdDate);
```

## Class Member Ordering

When generating or refactoring C# classes, order members consistently:

1. Nested types (only if needed)
2. Constants
3. Private fields
4. Properties
5. Constructors
6. Public methods
7. Protected methods
8. Private methods

### Example

```csharp
public class CustomerService
{
    private enum RetryStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Failed
    }

    private const int MaxRetryCount = 3;
    private readonly ICustomerRepository _customerRepository;
    private readonly ILogger<CustomerService> _logger;
    private int _retryCount;

    public string ServiceName { get; } = "Customer Service";
    private bool IsRetryEnabled { get; set; }

    public CustomerService(
        ICustomerRepository customerRepository,
        ILogger<CustomerService> logger)
    {
        _customerRepository = customerRepository;
        _logger = logger;
    }

    public async Task<Customer?> GetCustomerAsync(int customerId)
    {
        return await _customerRepository.GetCustomerByIdAsync(customerId);
    }

    protected virtual bool CanProcessCustomer(Customer customer)
    {
        return customer.IsActive;
    }

    private bool CanRetry()
    {
        return _retryCount < MaxRetryCount;
    }
}
```

## Required Behavior for AI Agent

When generating or refactoring C# code, apply these naming conventions automatically.
If existing code uses different naming, preserve compatibility **only** when renaming would break
public APIs, serialization contracts, database mappings, or external integrations.
