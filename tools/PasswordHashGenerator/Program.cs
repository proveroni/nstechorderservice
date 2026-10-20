
using Microsoft.AspNetCore.Identity;

var passwordHasher = new PasswordHasher<object>();

Console.Write("Informe a senha do usuário de demonstração: ");

var password = Console.ReadLine();

if (string.IsNullOrWhiteSpace(password))
{
    Console.WriteLine("A senha não pode ser vazia.");
    return;
}

var passwordHash = passwordHasher.HashPassword(
    new object(),
    password);

Console.WriteLine();
Console.WriteLine("Hash gerado:");
Console.WriteLine(passwordHash);
