﻿using DataTransfer;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BLL.Parsers.Implementations
{
    class RozetkaParser : IParser, IDisposable
    {
        private IWebDriver webDriver;
        private Microsoft.Extensions.Configuration.IConfiguration config;
        private ShopDto shop;
        private Regex keywordsRegex;
        private bool disposedValue;
        
        public RozetkaParser(
            Microsoft.Extensions.Configuration.IConfiguration config,
            ShopDto shop,
            Regex keywordsPattern,
            IWebDriver webDriver)
        {
            this.config = config;
            this.webDriver = webDriver;
            this.shop = shop;
            this.keywordsRegex = keywordsPattern;
        }

        public async Task<List<LotDto>> ParseLots(int pagesAmount)
        {
            Console.WriteLine("Parsing rozetka");
            List<LotDto> result = new List<LotDto>();
            await Task.Run(async () =>
            {
                string url = config["Parsing:Addresses:Rozetka.com.ua"];
                for (int i = 0; i < pagesAmount; i++)
                {
                    string pageUrl = $"{url};page={i + 1}";
                    webDriver.Navigate().GoToUrl(pageUrl);
                    await Task.Delay(1000);
                    if (i > 0 && !webDriver.Url.Contains("page"))
                        break;

                    var lotDivs = webDriver.FindElements(By.CssSelector("div[class=\"goods-tile__inner\"]"));
                    
                    await ScrollPage();

                    for (int j = 0; j < lotDivs.Count; j++)
                    {
                        var lotDiv = lotDivs[j];
                        var lot = ParseLot(lotDiv);

                        if (lot == null) continue;

                        result.Add(lot);
                    }
                }
            });
            return result;
        }

        private async Task ScrollPage()
        {
            IJavaScriptExecutor js = (IJavaScriptExecutor)webDriver;
            int height = 0;
            while (height <= 12800)
            {
                js.ExecuteScript("window.scrollTo (0, " + height + ")");
                height += 500;
                await Task.Delay(200);
            }
        }

        private LotDto ParseLot(IWebElement lotDiv)
        {
            try
            {
                var a = lotDiv.FindElement(By.CssSelector("a[class=\"goods-tile__picture\"]"));
                var imgElement = lotDiv.FindElement(By.CssSelector("img"));
                string title = imgElement.GetAttribute("title");
                if (!keywordsRegex.IsMatch(title))
                    return null;

                string link = new string(a.GetAttribute("href").TakeWhile(x => x != '?').ToArray());

                string imgLink = imgElement.GetAttribute("src");
                string priceStr = lotDiv.FindElement(By.CssSelector("span[class=\"goods-tile__price-value\"]")).Text;
                priceStr = new string(priceStr.Where(x => x != ' ').ToArray());
                decimal price = decimal.Parse(new string(priceStr.TakeWhile(x => Char.IsDigit(x)).ToArray()));
                int grams = ParsingUtils.GetGrams(title);

                return new LotDto()
                {
                    Shop = this.shop,
                    ImageLink = imgLink,
                    Link = link,
                    Manufacturer = null,
                    Title = title,
                    WeightInGrams = grams,
                    Price = new PriceDto()
                    {
                        Date = DateTime.Now,
                        Value = price
                    }
                };
            }
            catch (Exception ex)
            {
                return null;
            }
        }



        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    webDriver.Quit();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
