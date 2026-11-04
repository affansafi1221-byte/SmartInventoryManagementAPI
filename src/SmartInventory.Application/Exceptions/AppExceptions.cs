using System;
using System.Collections.Generic;
using System.Text;

namespace SmartInventory.Application.Exceptions;

public class ValidationException(string message) : Exception(message);
public class NotFoundException(string message) : Exception(message);

public class UnauthorizedException(string message) : Exception(message);
public class ConcurrencyException(string message) : Exception(message);
