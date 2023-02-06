using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;
using tinkoff2beancount;

string inputCsvFile = args[0];
string outputFile = args[1];

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

NumberFormatInfo numberFormatInfo = new NumberFormatInfo();
numberFormatInfo.NumberDecimalSeparator = ",";
var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = ";",
};
List<Transaction> transactions = new List<Transaction>();
using (StreamReader reader = new StreamReader(inputCsvFile, System.Text.Encoding.GetEncoding("windows-1251")))
using (CsvReader csvReader = new CsvReader(reader, csvConfig))
{
    var entries = csvReader.GetRecords<TransactionEntry>().ToList();
    Console.WriteLine($"{entries.Count} entries read");
    transactions.AddRange(entries.Select(ParseCsvEntry).Where(t => t.StatusOk).OrderBy(t => t.Date));
}

using (StreamWriter writer = new StreamWriter(outputFile))
{
    foreach (var t in transactions)
    {
        // Write header line
        writer.WriteLine($"{t.Date.ToString("yyyy-MM-dd")} * \"{t.Description}\"");
        // Write MCC if exists, otherwise 0
        writer.WriteLine($"  mcc: {t.Mcc ?? "0"}");
        // Write main expense account
        string account = t.CardNumber switch
        {
            "*2056" => "Assets:M:Bank:Tinkoff:Igor:Black",
            "*8791" => "Assets:M:Bank:Tinkoff:Olga:Black",
            "*7024" => "Assets:M:Bank:Tinkoff:Roman",
            _ => "??"
        };
        writer.WriteLine($"  {account}     {t.TotalValue.ToString("F2")} RUB");

        // Write category
        writer.WriteLine($"  Expenses:");
        writer.WriteLine();
    }
}

Transaction ParseCsvEntry(TransactionEntry transactionEntry)
{
    DateOnly date = DateOnly.FromDateTime(DateTime.Now);
    if (transactionEntry.Date is string d)
    {
        date = DateOnly.Parse(d);
    }

    decimal totalValue = 0.0M;
    if (transactionEntry.TotalValue is string v)
    {
        totalValue = Convert.ToDecimal(v, numberFormatInfo);
    }

    string category = "";
    if (transactionEntry.Category is string c)
    {
        category = c;
    }

    return new Transaction(date, transactionEntry.CardNumber, totalValue, category, transactionEntry.Mcc == "" ? null : transactionEntry.Mcc, transactionEntry.Description, transactionEntry.Status == "OK");
}