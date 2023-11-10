using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;
using tinkoff2beancount;
using Tomlyn;
using Tomlyn.Model;

string configFile = args[0];
string outputFile = args[1];

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var config = Toml.ToModel(File.ReadAllText(configFile, Encoding.UTF8));
TomlTable cardsTable = (TomlTable)config["cards"];
TomlTable categoriesTable = (TomlTable)config["categories"];
TomlTable dropStrings = (TomlTable)config["tinkoff_drop"];
TomlTable options = (TomlTable)config["options"];
decimal rTransferSum = decimal.Parse((string)options["r_transfer_sum"], CultureInfo.InvariantCulture);

NumberFormatInfo numberFormatInfo = new NumberFormatInfo();
numberFormatInfo.NumberDecimalSeparator = ",";
var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = ";",
};
List<Transaction> transactions = new List<Transaction>();
for (int argN = 2; argN < args.Length; argN++)
{
    using (StreamReader reader = new StreamReader(args[argN], System.Text.Encoding.GetEncoding("windows-1251")))
    {
        using (CsvReader csvReader = new CsvReader(reader, csvConfig))
        {
            var entries = csvReader.GetRecords<TransactionEntry>().ToList();
            Console.WriteLine($"{entries.Count} entries read");
            transactions.AddRange(
                entries.Select(ParseCsvEntry).Where(t => t.StatusOk).OrderBy(t => t.Date));
        }
    }
}

using StreamWriter writer = new StreamWriter(outputFile, false, Encoding.UTF8);
for (int i = 0; i < transactions.Count; i++)
{
    Transaction t = transactions[i];
    Transaction? nextT = i == transactions.Count - 1 ? null : transactions[i + 1];
    
    if (dropStrings.Values.Any(v => t.Description?.Contains((string)v) ?? false))
    {
        Console.WriteLine($"dropped: {t}");
        continue;
    }
    if ((t.Description?.Contains("ман Л") ?? false) && t.TotalValue == rTransferSum)
    {
        Console.WriteLine($"dropped: {t}");
        continue;
    }
    // Если две подряд транзакции с одинаковой суммой, но разными знаками, то убираем одну, т.к. это перевод
    if (t.Description == "Перевод между счетами" && nextT != null)
    {
        if (t.TotalValue == -nextT.TotalValue
            && t.Description == nextT.Description
            && t.Category == "Переводы")
        {
            Console.WriteLine($"dropped transfer: {t}");
            continue;
        }
    }
        

    // Write header line
    string headerDescription = t.Description ?? "Прочее";
    // Write MCC if exists, otherwise 0
    string mcc = t.Mcc ?? "0";
    // Write main expense account
    string? cardNumber = t.CardNumber?.TrimStart('*');
    if ((t.Description?.Contains("ман Л") ?? false) && t.TotalValue == -rTransferSum)
    {
        cardNumber = "2056";
    }
    else if (t.Description == "Перевод между счетами" && t.TotalValue == -rTransferSum)
    {
        cardNumber = "2056";
    } 

    if (t.Description?.Contains("Подписка Tinkoff pro") ?? false)
    {
        cardNumber = "2056";
    }

    if (t.Description == "Тинькофф Топливо")
    {
        cardNumber = "2056";
    }
    
    if (cardNumber == null || !cardsTable.TryGetValue(cardNumber, out object account))
    {
        account = "XX";
    }

    // Write category
    object category = "YY";
    if (t.Description == "Перевод между счетами" && t.TotalValue == -rTransferSum)
    {
        category = cardsTable["7024"];
        headerDescription = "Перевод Роме";
    }
    else if (t.Description == "Подписка Tinkoff pro")
    {
        category = categoriesTable["tinkoff_pro"];
    }
    else if (t.Description?.Contains("Кэшбэк за") ?? false)
    {
        category = categoriesTable["tinkoff_cashback"];
    }
    else if (t.Description?.Contains("Проценты на остаток") ?? false)
    {
        category = categoriesTable["tinkoff_interest"];
    }
    else if (t is { TotalValue: < 0, Description: not null } 
             && categoriesTable.TryGetValue(t.Description, out var foundCategory))
    {
        category = foundCategory;
    }    
    else if (t.TotalValue > 0)
    {
        category = "Income:";
    }
    else
    {
        category = "Expenses:";
    }
    
    writer.WriteLine($"{t.Date.ToString("yyyy-MM-dd")} * \"{headerDescription}\"");
    writer.WriteLine($"  mcc: {mcc}");
    writer.WriteLine($"  {account}     {t.TotalValue.ToString("F2", CultureInfo.InvariantCulture)} RUB");
    writer.WriteLine($"  {category}");
    writer.WriteLine();
}

Transaction ParseCsvEntry(TransactionEntry transactionEntry)
{
    DateOnly date = DateOnly.FromDateTime(DateTime.Now);
    if (transactionEntry.Date is { } d)
    {
        date = DateOnly.Parse(d);
    }

    decimal totalValue = 0.0M;
    if (transactionEntry.TotalValue is { } v)
    {
        totalValue = Convert.ToDecimal(v, numberFormatInfo);
    }

    string category = "";
    if (transactionEntry.Category is { } c)
    {
        category = c;
    }

    return new Transaction(date, transactionEntry.CardNumber, totalValue, category,
        transactionEntry.Mcc == "" ? null : transactionEntry.Mcc, transactionEntry.Description,
        transactionEntry.Status == "OK");
}