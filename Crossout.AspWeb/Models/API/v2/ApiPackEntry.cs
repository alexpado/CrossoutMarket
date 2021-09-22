﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crossout.Data.Descriptions;
using Crossout.Data.Stats.Main;
using Newtonsoft.Json;
using Crossout.Model;
using Crossout.Model.Formatter;
using Crossout.Data.PremiumPackages;
using Crossout.AspWeb.Models.General;
using Crossout.AspWeb.Pocos;

namespace Crossout.AspWeb.Models.API.v2
{
    public class ApiPackEntry : ApiEntryBase
    {
        [JsonProperty("key")]
        public string Key;

        [JsonProperty("containeditems")]
        public List<ContainedItemNew> ContainedItems;

        [JsonProperty("sellsum")]
        public int SellSum;

        [JsonProperty("buysum")]
        public int BuySum;

        [JsonProperty("formatsellsum")]
        public string FormatSellSum => PriceFormatter.FormatPrice(SellSum);

        [JsonProperty("formatbuysum")]
        public string FormatBuySum => PriceFormatter.FormatPrice(BuySum);

        [JsonProperty("totalsellsum")]
        public int TotalSellSum => SellSum + RawCoins * 100;

        [JsonProperty("totalbuysum")]
        public int TotalBuySum => BuySum + RawCoins * 100;

        [JsonProperty("formattotalsellsum")]
        public string FormatTotalSellSum => PriceFormatter.FormatPrice(TotalSellSum);

        [JsonProperty("formattotalbuysum")]
        public string FormatTotalBuySum => PriceFormatter.FormatPrice(TotalBuySum);

        [JsonProperty("rawcoins")]
        public int RawCoins;

        [JsonProperty("appprices")]
        public SteamPricesPoco AppPrices;

        public ApiPackEntry(PremiumPackageNew package)
        {
            package.Create();
            Id = package.Id;
            Name = package.Name;
            Key = package.Key;
            ContainedItems = package.ContainedItems;
            SellSum = package.SellSum;
            BuySum = package.BuySum;
            RawCoins = package.RawCoins;
            AppPrices = package.AppPrices;
            AppPrices.Prices.ForEach(x => x.CurrencyAbbriviation = x.CurrencyAbbriviation.ToLower());
        }
    }

    public class AppPrices
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("steamappid")]
        public int SteamAppId;

        [JsonProperty("prices")]
        public List<Currency> Prices;

        [JsonProperty("discount")]
        public int Discount;

        [JsonProperty("successtimestamp")]
        public DateTime SuccessTimestamp;

        public void Create(object[] row)
        {
            Prices = new List<Currency>();
            Id = row[0].ConvertTo<int>();
            SteamAppId = row[1].ConvertTo<int>();
            Prices.Add(new Currency
            {
                CurrencyAbbriviation = "usd",
                Final = row[2].ConvertTo<int>()
            });
            Prices.Add(new Currency
            {
                CurrencyAbbriviation = "eur",
                Final = row[3].ConvertTo<int>()
            });
            Prices.Add(new Currency
            {
                CurrencyAbbriviation = "gbp",
                Final = row[4].ConvertTo<int>()
            });
            Prices.Add(new Currency
            {
                CurrencyAbbriviation = "rub",
                Final = row[5].ConvertTo<int>()
            });
            Discount = row[6].ConvertTo<int>();
            SuccessTimestamp = row[7].ConvertTo<DateTime>();
        }
    }

    public class Currency
    {
        [JsonProperty("currencyabbriviation")]
        public string CurrencyAbbriviation;

        [JsonProperty("final")]
        public int Final;

        [JsonProperty("formatfinal")]
        public string FormatFinal => PriceFormatter.FormatPrice(Final);
    }

    public class ContainedItem
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("sellprice")]
        public int SellPrice;

        [JsonProperty("buyprice")]
        public int BuyPrice;

        [JsonProperty("formatsellprice")]
        public string FormatSellPrice => PriceFormatter.FormatPrice(SellPrice);

        [JsonProperty("formatbuyprice")]
        public string FormatBuyPrice => PriceFormatter.FormatPrice(BuyPrice);
    }
}
