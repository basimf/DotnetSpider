using DotnetSpider.Core;
using System.Collections.Generic;
using System.IO;
using LogLevel = DotnetSpider.Core.LogLevel;
#if !NET_CORE
using OpenQA.Selenium.Firefox;
using System;
using DotnetSpider.Core.Selector;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using DotnetSpider.Core.Common;
using DotnetSpider.Extension.Model;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Remote;
#endif

namespace DotnetSpider.Extension.Downloader
{
	public enum WebBrowser
	{
		Chrome,
		Firefox
	}
	public class SiteCookie
	{
		public string CookiesStringPart { get; set; }
		public Dictionary<string, string> CookiesDictionary = new Dictionary<string, string>();
	}

	public abstract class CookieInterceptor : Named
	{
		public abstract SiteCookie GetCookie();
	}

	public class DataBaseCookieInterceptor : CookieInterceptor
	{
		public override SiteCookie GetCookie()
		{
			return new SiteCookie();
		}
	}

#if !NET_CORE
	public abstract class WebDriverCookieInterceptor : CookieInterceptor
	{
		public WebBrowser Browser { get; set; } = WebBrowser.Chrome;
		protected IWebElement FindElement(RemoteWebDriver webDriver, Selector element)
		{
			switch (element.Type)
			{

				case SelectorType.XPath:
					{
						return webDriver.FindElementByXPath(element.Expression);
					}
				case SelectorType.Css:
					{
						return webDriver.FindElementByCssSelector(element.Expression);
					}
			}
			throw new SpiderException("Unsport findy: " + element.Type);
		}

		protected RemoteWebDriver GetWebDriver()
		{
			RemoteWebDriver webDriver = null;
			switch (Browser)
			{
				case WebBrowser.Chrome:
					{
						ChromeDriverService cds = ChromeDriverService.CreateDefaultService();
						cds.HideCommandPromptWindow = true;
						ChromeOptions opt = new ChromeOptions();
						opt.AddUserProfilePreference("profile", new { default_content_setting_values = new { images = 2 } });
						webDriver = new ChromeDriver(cds, opt);
						break;
					}
				case WebBrowser.Firefox:
					{
						string path = System.Environment.ExpandEnvironmentVariables("%APPDATA%") + @"\Mozilla\Firefox\Profiles\";
						string[] pathsToProfiles = Directory.GetDirectories(path, "*.webdriver", SearchOption.TopDirectoryOnly);
						if (pathsToProfiles.Length == 1)
						{
							FirefoxProfile profile = new FirefoxProfile(pathsToProfiles[0], false);
							profile.AlwaysLoadNoFocusLibrary = true;
							webDriver = new FirefoxDriver(profile);
						}
						else
						{
							throw new Exception("No Firefox profiles: webdriver.");
						}
						break;
					}
				default:
					{
						throw new Exception("Unsupported browser!");
					}
			}

			webDriver.Manage().Window.Maximize();
			return webDriver;
		}
	}

	public class FiddlerLoginCookieInterceptor : CommonCookieInterceptor
	{
		public int ProxyPort { get; set; } = 30000;
		public string Pattern { get; set; }

		public override SiteCookie GetCookie()
		{
			if (string.IsNullOrEmpty(Pattern))
			{
				throw new Exception("Fiddler CookieTrapper: Pattern cannot be null!");
			}

			string cookie;
			using (FiddlerClient fiddlerWrapper = new FiddlerClient(ProxyPort, Pattern))
			{
				fiddlerWrapper.StartCapture(true);
				try
				{
					base.GetCookie();
					var header = fiddlerWrapper.Headers;
					const string cookiesPattern = @"Cookie: (.*?)\r\n";
					cookie = Regex.Match(header, cookiesPattern).Groups[1].Value;
				}
				catch (Exception e)
				{
					LogCenter.Log(null, "Get cookie failed.", LogLevel.Error, e);
					return null;
				}
				fiddlerWrapper.StopCapture();
			}

			return new SiteCookie { CookiesStringPart = cookie };
		}
	}

	public class CommonCookieInterceptor : WebDriverCookieInterceptor
	{
		public string Url { get; set; }

		public string AfterLoginUrl { get; set; }

		public Selector UserSelector { get; set; }

		public string User { get; set; }

		public Selector PassSelector { get; set; }

		public string Pass { get; set; }

		public Selector SubmitSelector { get; set; }

		public Selector LoginAreaSelector { get; set; }

		public override SiteCookie GetCookie()
		{
			var cookies = new Dictionary<string, string>();

			var webDriver = GetWebDriver();
			try
			{
				webDriver.Navigate().GoToUrl(Url);
				Thread.Sleep(10000);

				if (LoginAreaSelector != null)
				{
					try
					{
						var loginArea = FindElement(webDriver, LoginAreaSelector);
						loginArea.Click();
						Thread.Sleep(1000);
					}
					catch (Exception e)
					{
						// ignored
					}
				}

				if (UserSelector != null)
				{
					var user = FindElement(webDriver, UserSelector);
					user.Clear();
					user.SendKeys(User);
					Thread.Sleep(1500);
				}

				if (PassSelector != null)
				{
					var pass = FindElement(webDriver, PassSelector);
					pass.Clear();
					pass.SendKeys(Pass);
					Thread.Sleep(1500);
				}

				var submit = FindElement(webDriver, SubmitSelector);
				submit.Click();
				Thread.Sleep(10000);

				if (!string.IsNullOrEmpty(AfterLoginUrl))
				{
					webDriver.Navigate().GoToUrl(AfterLoginUrl);
					Thread.Sleep(10000);
				}

				var cookieList = webDriver.Manage().Cookies.AllCookies.ToList();
				if (cookieList.Count > 0)
				{
					foreach (var cookieItem in cookieList)
					{
						cookies.Add(cookieItem.Name, cookieItem.Value);
					}
				}

				webDriver.Dispose();
			}
			catch (Exception e)
			{
				LogCenter.Log(null, "Get cookie failed.", LogLevel.Error, e);
				webDriver.Dispose();
				return null;
			}

			return new SiteCookie { CookiesDictionary = cookies };
		}
	}
#endif
}
