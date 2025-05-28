using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using System.Numerics;

namespace PsiOrleans.Plugins;

/// <summary>
/// Semantic Kernel plugin for mathematical operations on integers
/// </summary>
public class MathematicalOperationsPlugin
{
    [KernelFunction, Description("Perform basic arithmetic operations on two integers")]
    public async Task<string> BasicArithmetic(
        [Description("The first integer operand")] long a,
        [Description("The second integer operand")] long b,
        [Description("The operation to perform: 'add', 'subtract', 'multiply', 'divide', 'modulo'")] string operation)
    {
        await Task.Delay(10); // Simulate processing time

        try
        {
            var result = operation.ToLower() switch
            {
                "add" => a + b,
                "subtract" => a - b,
                "multiply" => a * b,
                "divide" => b != 0 ? a / b : throw new DivideByZeroException("Cannot divide by zero"),
                "modulo" => b != 0 ? a % b : throw new DivideByZeroException("Cannot perform modulo with zero"),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };

            return JsonSerializer.Serialize(new
            {
                operation = operation.ToLower(),
                operand_a = a,
                operand_b = b,
                result = result,
                formula = $"{a} {GetOperatorSymbol(operation)} {b} = {result}",
                timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error in basic arithmetic: {ex.Message}");
        }
    }

    [KernelFunction, Description("Calculate power and root operations")]
    public async Task<string> PowerAndRoot(
        [Description("The base number")] long baseNumber,
        [Description("The exponent or root index")] int exponent,
        [Description("The operation: 'power', 'nthroot'")] string operation)
    {
        await Task.Delay(20);

        try
        {
            var result = operation.ToLower() switch
            {
                "power" => (long)Math.Pow(baseNumber, exponent),
                "nthroot" => exponent > 0 ? (long)Math.Pow(baseNumber, 1.0 / exponent) : throw new ArgumentException("Root index must be positive"),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };

            return JsonSerializer.Serialize(new
            {
                operation = operation.ToLower(),
                base_number = baseNumber,
                exponent_or_index = exponent,
                result = result,
                formula = operation.ToLower() == "power" ? $"{baseNumber}^{exponent} = {result}" : $"{exponent}√{baseNumber} = {result}",
                timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error in power/root operation: {ex.Message}");
        }
    }

    [KernelFunction, Description("Calculate factorial and combinatorics")]
    public async Task<string> FactorialAndCombinatorics(
        [Description("The main number (n)")] int n,
        [Description("The second number for combinations/permutations (r), optional for factorial")] int r = 0,
        [Description("The operation: 'factorial', 'combination', 'permutation'")] string operation = "factorial")
    {
        await Task.Delay(30);

        try
        {
            if (n < 0) throw new ArgumentException("Numbers must be non-negative");

            var result = operation.ToLower() switch
            {
                "factorial" => CalculateFactorial(n),
                "combination" => r >= 0 && r <= n ? CalculateFactorial(n) / (CalculateFactorial(r) * CalculateFactorial(n - r)) : throw new ArgumentException("Invalid combination parameters"),
                "permutation" => r >= 0 && r <= n ? CalculateFactorial(n) / CalculateFactorial(n - r) : throw new ArgumentException("Invalid permutation parameters"),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };

            var formula = operation.ToLower() switch
            {
                "factorial" => $"{n}! = {result}",
                "combination" => $"C({n},{r}) = {n}!/({r}!×{n-r}!) = {result}",
                "permutation" => $"P({n},{r}) = {n}!/({n-r}!) = {result}",
                _ => ""
            };

            return JsonSerializer.Serialize(new
            {
                operation = operation.ToLower(),
                n = n,
                r = operation.ToLower() != "factorial" ? r : (int?)null,
                result = result,
                formula = formula,
                timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error in factorial/combinatorics: {ex.Message}");
        }
    }

    [KernelFunction, Description("Prime number operations")]
    public async Task<string> PrimeOperations(
        [Description("The number to check or range start")] int number,
        [Description("The operation: 'isprime', 'primesinrange', 'primefactors'")] string operation,
        [Description("End of range for 'primesinrange' operation")] int rangeEnd = 0)
    {
        await Task.Delay(50);

        try
        {
            object result = operation.ToLower() switch
            {
                "isprime" => new { is_prime = IsPrime(number), number = number },
                "primesinrange" => new { primes = FindPrimesInRange(number, rangeEnd > number ? rangeEnd : number + 10), range_start = number, range_end = rangeEnd > number ? rangeEnd : number + 10 },
                "primefactors" => new { prime_factors = GetPrimeFactors(number), number = number },
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };

            return JsonSerializer.Serialize(new
            {
                operation = operation.ToLower(),
                input_number = number,
                range_end = operation.ToLower() == "primesinrange" ? (rangeEnd > number ? rangeEnd : number + 10) : (int?)null,
                result = result,
                timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error in prime operations: {ex.Message}");
        }
    }

    [KernelFunction, Description("Generate mathematical sequences")]
    public async Task<string> SequenceOperations(
        [Description("The sequence type: 'fibonacci', 'arithmetic', 'geometric'")] string sequenceType,
        [Description("Number of terms to generate")] int terms,
        [Description("First term (for arithmetic/geometric sequences)")] long firstTerm = 1,
        [Description("Common difference (arithmetic) or ratio (geometric)")] long commonValue = 1)
    {
        await Task.Delay(40);

        try
        {
            if (terms <= 0) throw new ArgumentException("Number of terms must be positive");

            var sequence = sequenceType.ToLower() switch
            {
                "fibonacci" => GenerateFibonacci(terms),
                "arithmetic" => GenerateArithmetic(terms, firstTerm, commonValue),
                "geometric" => GenerateGeometric(terms, firstTerm, commonValue),
                _ => throw new ArgumentException($"Unknown sequence type: {sequenceType}")
            };

            return JsonSerializer.Serialize(new
            {
                sequence_type = sequenceType.ToLower(),
                terms_count = terms,
                first_term = sequenceType.ToLower() != "fibonacci" ? firstTerm : (long?)null,
                common_value = sequenceType.ToLower() != "fibonacci" ? commonValue : (long?)null,
                sequence = sequence,
                sum = sequence.Sum(),
                timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error in sequence operations: {ex.Message}");
        }
    }

    [KernelFunction, Description("Perform complex multi-step mathematical calculations")]
    public async Task<string> ComplexCalculation(
        [Description("Mathematical expression description (e.g., 'calculate 5! + 2^8 - fibonacci(10)')")] string expression,
        [Description("List of numbers involved in the calculation")] string numbersJson = "[]")
    {
        await Task.Delay(100);

        try
        {
            // Parse numbers from JSON if provided
            var numbers = JsonSerializer.Deserialize<List<int>>(numbersJson) ?? new List<int>();
            
            // This is a simplified complex calculation example
            // In a real implementation, you'd parse and evaluate the expression
            var steps = new List<object>();
            long finalResult = 0;

            // Example complex calculation: factorial of first number + power of second^third + fibonacci sum
            if (numbers.Count >= 3)
            {
                var factorial = CalculateFactorial(numbers[0]);
                steps.Add(new { step = 1, operation = "factorial", input = numbers[0], result = factorial });

                var power = (long)Math.Pow(numbers[1], numbers[2]);
                steps.Add(new { step = 2, operation = "power", base_num = numbers[1], exponent = numbers[2], result = power });

                var fibonacci = GenerateFibonacci(Math.Min(numbers[0], 20)); // Limit to prevent overflow
                var fibSum = fibonacci.Sum();
                steps.Add(new { step = 3, operation = "fibonacci_sum", terms = Math.Min(numbers[0], 20), result = fibSum });

                finalResult = factorial + power + fibSum;
                steps.Add(new { step = 4, operation = "final_sum", formula = $"{factorial} + {power} + {fibSum}", result = finalResult });
            }
            else
            {
                // Simple calculation if not enough numbers
                finalResult = numbers.Sum();
                steps.Add(new { step = 1, operation = "sum", numbers = numbers, result = finalResult });
            }

            return JsonSerializer.Serialize(new
            {
                expression_description = expression,
                input_numbers = numbers,
                calculation_steps = steps,
                final_result = finalResult,
                complexity_level = steps.Count > 2 ? "high" : "low",
                timestamp = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error in complex calculation: {ex.Message}");
        }
    }

    // Helper methods
    private string GetOperatorSymbol(string operation) => operation.ToLower() switch
    {
        "add" => "+",
        "subtract" => "-",
        "multiply" => "×",
        "divide" => "÷",
        "modulo" => "%",
        _ => operation
    };

    private long CalculateFactorial(int n)
    {
        if (n > 20) throw new ArgumentException("Factorial too large (max 20)");
        long result = 1;
        for (int i = 2; i <= n; i++)
            result *= i;
        return result;
    }

    private bool IsPrime(int number)
    {
        if (number < 2) return false;
        if (number == 2) return true;
        if (number % 2 == 0) return false;

        for (int i = 3; i * i <= number; i += 2)
        {
            if (number % i == 0) return false;
        }
        return true;
    }

    private List<int> FindPrimesInRange(int start, int end)
    {
        var primes = new List<int>();
        for (int i = Math.Max(2, start); i <= end && i <= 1000; i++) // Limit to prevent performance issues
        {
            if (IsPrime(i)) primes.Add(i);
        }
        return primes;
    }

    private List<int> GetPrimeFactors(int number)
    {
        var factors = new List<int>();
        int divisor = 2;
        
        while (divisor * divisor <= number)
        {
            while (number % divisor == 0)
            {
                factors.Add(divisor);
                number /= divisor;
            }
            divisor++;
        }
        
        if (number > 1) factors.Add(number);
        return factors;
    }

    private List<long> GenerateFibonacci(int terms)
    {
        var sequence = new List<long>();
        if (terms >= 1) sequence.Add(0);
        if (terms >= 2) sequence.Add(1);
        
        for (int i = 2; i < terms && i < 50; i++) // Limit to prevent overflow
        {
            sequence.Add(sequence[i - 1] + sequence[i - 2]);
        }
        
        return sequence.Take(terms).ToList();
    }

    private List<long> GenerateArithmetic(int terms, long firstTerm, long commonDifference)
    {
        var sequence = new List<long>();
        for (int i = 0; i < terms; i++)
        {
            sequence.Add(firstTerm + i * commonDifference);
        }
        return sequence;
    }

    private List<long> GenerateGeometric(int terms, long firstTerm, long commonRatio)
    {
        var sequence = new List<long>();
        long current = firstTerm;
        for (int i = 0; i < terms; i++)
        {
            sequence.Add(current);
            current *= commonRatio;
        }
        return sequence;
    }

    private string CreateErrorResponse(string message)
    {
        return JsonSerializer.Serialize(new
        {
            error = true,
            message = message,
            timestamp = DateTime.UtcNow
        }, new JsonSerializerOptions { WriteIndented = true });
    }
} 