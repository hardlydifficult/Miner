﻿using HD.Data.JSON;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HD.Controllers
{
  public static class CurrencyExchangeManager
  {
    const string API_URL = "https://api.fixer.io/latest?base=";

    static Dictionary<string, (DateTime lastUpdated, CurrencyExchangeRates values)> rates = new Dictionary<string, (DateTime, CurrencyExchangeRates)>();
    static int isUpdating = 0;

    public static CurrencyExchange From(decimal amount, HD.Currency baseCurrency, bool forceUpdate = false)
    {
      var currencyName = baseCurrency.ToString();
      if (AreRatesOutdated(currencyName) || forceUpdate)
      {
        Task.Run(() => { Fetch(currencyName); });
      }

      // to avoid a race condition when returning at the exact same time
      // as the update thread is writing to the dict.
      lock (rates)
      {
        return rates.ContainsKey(currencyName) ?
          new CurrencyExchange(rates[currencyName].values, amount) :
          new CurrencyExchange(null, -1);
      }
    }

    private static bool AreRatesOutdated(string baseCurrency)
    {
      if (!rates.ContainsKey(baseCurrency))
        return true;

      // Update once per hour
      if ((DateTime.UtcNow - rates[baseCurrency].lastUpdated).TotalHours >= 1)
        return true;

      return false;
    }

    private static void Fetch(string baseCurrency)
    {
      if (System.Threading.Interlocked.CompareExchange(ref isUpdating, 1, 0) == 0)
      {
        try
        {
          var dataString = Encoding.UTF8.GetString(HDWebClient.GetBytes($"{API_URL}{baseCurrency}"));
          var obj = JsonConvert.DeserializeObject<CurrencyExchangeRates>(dataString);
          lock (rates)
          {
            rates[baseCurrency] = (lastUpdated: DateTime.UtcNow, values: obj);
          }
        }
        catch
        {
        }
        finally
        {
          isUpdating = 0;
        }
      }
    }
  }
}
