using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;

namespace Nop.Plugin.Payments.Skrill.Services
{
    /// <summary>
    /// Represents the HTTP client to request third-party services
    /// </summary>
    public partial class ServiceHttpClient
    {
        #region Fields

        private readonly HttpClient _httpClient;

        #endregion

        #region Ctor

        public ServiceHttpClient(HttpClient client)
        {
            //configure client
            client.Timeout = TimeSpan.FromMilliseconds(10000);
            client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, Defaults.UserAgent);
            client.DefaultRequestHeaders.Add(HeaderNames.Accept, "*/*");

            _httpClient = client;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Request a service and get the response
        /// </summary>
        /// <param name="url">Service URL</param>
        /// <returns>The asynchronous task whose result contains the response data</returns>
        public async Task<string> GetAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                return await response.Content.ReadAsStringAsync();
            }
            catch (AggregateException exception)
            {
                //rethrow the actual exception
                throw exception.InnerException ?? exception;
            }
        }

        #endregion
    }
}