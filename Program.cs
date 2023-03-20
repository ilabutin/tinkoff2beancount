using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;
using tinkoff2beancount;
using Tomlyn;
using Tomlyn.Model;

string configFile = args[0];
string inputCsvFile = args[1];
string outputFile = args[2];

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var config = Toml.ToModel(File.ReadAllText(configFile, Encoding.UTF8));

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

TomlTable cardsTable = (TomlTable)config["cards"];
TomlTable categoriesTable = (TomlTable)config["categories"];

using (StreamWriter writer = new StreamWriter(outputFile, false, Encoding.UTF8))
{
    foreach (var t in transactions)
    {
        // Write header line
        writer.WriteLine($"{t.Date.ToString("yyyy-MM-dd")} * \"{t.Description}\"");
        // Write MCC if exists, otherwise 0
        writer.WriteLine($"  mcc: {t.Mcc ?? "0"}");
        // Write main expense account
        string? cardNumber = t.CardNumber?.TrimStart('*');
        if (cardNumber == null || !cardsTable.TryGetValue(cardNumber, out object account))
        {
            account = "XX";
        }
        writer.WriteLine($"  {account}     {t.TotalValue.ToString("F2", CultureInfo.InvariantCulture)} RUB");

        // Write category
        if (t.Description == "Перевод между счетами")
        {
            writer.WriteLine($"  YY");
        }
        else if (t.Description?.Contains("Кэшбэк за") ?? false)
        {
            writer.WriteLine($"  {categoriesTable["tinkoff_cashback"]}");
        }
        else if (t.Description?.Contains("Проценты на остаток") ?? false)
        {
            writer.WriteLine($"  {categoriesTable["tinkoff_interest"]}");
        }
        else if (t.TotalValue > 0)
        {
            writer.WriteLine($"  Income:");
        }
        else
        {
            writer.WriteLine($"  Expenses:");
        }
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