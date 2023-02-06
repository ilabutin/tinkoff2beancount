using CsvHelper.Configuration.Attributes;

namespace tinkoff2beancount
{
    /// <summary>
    /// Transaction entry from CSV file
    /// </summary>
    internal class TransactionEntry
    {
        [Name("Дата платежа")]
        public string? Date { get; set; }
        [Name("Номер карты")]
        public string? CardNumber { get; set; }
        [Name("Сумма операции")]
        public string? TotalValue { get; set; }
        [Name("Категория")]
        public string? Category { get; set; }
        [Name("MCC")]
        public string? Mcc { get; set; }
        [Name("Описание")]
        public string? Description { get; set; }
        [Name("Статус")]
        public string? Status { get; set; }
    }
}
