namespace AccountsService.Domain.Models;

public class Bank
{
    public string Name { get; }
    public string Branch { get; }
    public string IfscCode { get; }

    public Bank(string name, string branch, string ifscCode)
    {
        Name = name;
        Branch = branch;
        IfscCode = ifscCode;
    }
}
