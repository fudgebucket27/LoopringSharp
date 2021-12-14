﻿using Newtonsoft.Json;
using PoseidonSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using static LoopringAPI.ApiTransferRequest;

namespace LoopringAPI
{
    public class SecureClient
    {
        string _apiUrl;
        string _exchange;
        HttpClient _client;
        public SecureClient(bool useTestNet)
        {
            // TODO: Replace with an api call to get all of them from the exchange
            LoadTokenMapper();

            _client = new HttpClient();
            if (useTestNet)
            {
                _apiUrl = "https://uat2.loopring.io/";
            }
            else
            {
                _apiUrl = "https://api3.loopring.io/";
            }

            _exchange = ExchangeInfo().Result.exchangeAddress;
        }

        #region NoAuthentication
        /// <summary>
        /// Gets the current exchange prices between varius cryptos on the Loopring Protocol
        /// </summary>        
        /// <param name="pairs">The tickers to retreive. (Ex. LRC-USDT, LRC-ETH)</param>
        /// <returns>Returns a list of all the ticker details for your requested tickers</returns>
        /// <exception cref="System.Exception">Gets thrown when there's a problem getting info from the Loopring API endpoint</exception>
        public async Task<List<Ticker>> Ticker(params string[] pairs)
        {
            string url = $"{_apiUrl}{Constants.TickerUrl}?market={string.Join(",", pairs)}";
            using (var httpResult = await _client.GetAsync(url))
            {
                _ = await ThrowIfHttpFail(httpResult);
                var resultBody = await httpResult.Content.ReadAsStringAsync();
                var apiTickersResult = JsonConvert.DeserializeObject<ApiTickersResult>(resultBody);

                return apiTickersResult.tickers.Select(s => new Ticker()
                {
                    PairId = s[0],
                    TimeStamp = s[1],
                    BaseTokenVolume = s[2],
                    QuoteTokenVolume = s[3],
                    OpenPrice = s[4],
                    HeighestPrice = s[5],
                    LowestPrice = s[6],
                    ClosingPrice = s[7],
                    NumberOfTrades = s[8],
                    HighestBidPrice = s[9],
                    LowestAskPrice = s[10],
                    BaseFeeAmmount = s[11],
                    QuoteFeeAmount = s[12]
                }).ToList();
            }
        }

        /// <summary>
        /// Returns the relayer's current time in millisecond
        /// </summary>
        /// <returns>Current time in milliseconds</returns>
        /// <exception cref="System.Exception">Gets thrown when there's a problem getting info from the Loopring API endpoint</exception>
        public async Task<long> Timestamp()
        {
            var url = $"{_apiUrl}{Constants.TimestampUrl}";
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Get, url))
            {
                using (var httpResult = await _client.SendAsync(httpRequest))
                {
                    _ = await ThrowIfHttpFail(httpResult);
                    var resultBody = await httpResult.Content.ReadAsStringAsync();
                    var apiresult = JsonConvert.DeserializeObject<ApiTimestampResult>(resultBody);
                    return apiresult.timestamp;
                }
            }
        }

        /// <summary>
        /// Return various configurations of Loopring.io
        /// </summary>
        /// <returns>Fees, exchange address, all sort of useful stuff</returns>
        public async Task<ExchangeInfo> ExchangeInfo()
        {
            var url = $"{_apiUrl}{Constants.ExchangeInfo}";
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Get, url))
            {
                using (var httpResult = await _client.SendAsync(httpRequest))
                {
                    _ = await ThrowIfHttpFail(httpResult);
                    var resultBody = await httpResult.Content.ReadAsStringAsync();
                    var apiresult = JsonConvert.DeserializeObject<ApiExchangeInfoResult>(resultBody);
                    return new ExchangeInfo()
                    {
                        ammExitFees = apiresult.ammExitFees,
                        chainId = apiresult.chainId,
                        depositAddress = apiresult.depositAddress,
                        exchangeAddress = apiresult.exchangeAddress,
                        fastWithdrawalFees = apiresult.fastWithdrawalFees,
                        onchainFees = apiresult.onchainFees,
                        openAccountFees = apiresult.openAccountFees,
                        transferFees = apiresult.transferFees,
                        updateFees = apiresult.updateFees,
                        withdrawalFees = apiresult.withdrawalFees
                    };
                }
            }
        }

        #endregion
        #region L2

        /// <summary>
        /// Get the ApiKey associated with the user's account.
        /// </summary>
        /// <param name="l2Pk">Wallet Layer 2 Private Key</param>
        /// <param name="accountId">The user's account Id</param>
        /// <returns>The api key</returns>
        /// <exception cref="System.Exception">Gets thrown when there's a problem getting info from the Loopring API endpoint</exception>
        public async Task<string> ApiKey(string l2Pk, int accountId)
        {
            var signedMessage = EddsaSignUrl(
                l2Pk,
                HttpMethod.Get,
                new List<(string Key, string Value)>() { ("accountId", accountId.ToString()) },
                null,
                Constants.ApiKeyUrl);

            var url = $"{_apiUrl}{Constants.ApiKeyUrl}?accountId={accountId}";
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Get, url))
            {
                httpRequest.Headers.Add("X-API-SIG", signedMessage);
                using (var httpResult = await _client.SendAsync(httpRequest))
                {
                    _ = await ThrowIfHttpFail(httpResult);
                    var resultBody = await httpResult.Content.ReadAsStringAsync();
                    var apiresult = JsonConvert.DeserializeObject<ApiApiKeyResult>(resultBody);
                    return apiresult.apiKey;
                }
            }
        }

        #endregion
        #region apiKeyL2

        /// <summary>
        /// Submit an order to exchange two currencies, but with all the nonsense removed
        /// </summary>
        /// <param name="l2Pk">Loopring Private Key</param>
        /// <param name="apiKey">Current Loopring API Key</param>
        /// <param name="accountId">Wallet Account Id</param>
        /// <param name="orderHash">The hash of the order you wish to nuke.</param>
        /// <param name="clientOrderId">The unique order ID of the client</param>
        /// <returns>Returns OrderResult which basically contains the status of your transaction after the cancel was succesfully requested</returns>
        public async Task<OrderResult> DeleteOrder(string l2Pk, string apiKey, int accountId, string orderHash, string clientOrderId)
        {
            var signedMessage = EddsaSignUrl(
                l2Pk,
                HttpMethod.Delete,
                new List<(string Key, string Value)>() { ("accountId", accountId.ToString()), ("clientOrderId", clientOrderId), ("orderHash", orderHash) },
                null,
                Constants.OrderUrl);

            var url = $"{_apiUrl}{Constants.OrderUrl}?accountId={accountId}&clientOrderId={clientOrderId}&orderHash={orderHash}";
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Delete, url))
            {
                httpRequest.Headers.Add("X-API-SIG", signedMessage);
                httpRequest.Headers.Add("X-API-KEY", apiKey);
                using (var httpResult = await _client.SendAsync(httpRequest))
                {
                    _ = await ThrowIfHttpFail(httpResult);
                    var resultBody = await httpResult.Content.ReadAsStringAsync();
                    var apiresult = JsonConvert.DeserializeObject<ApiOrderSubmitResult>(resultBody);
                    return new OrderResult(apiresult);
                }
            }
        }


        /// <summary>
        /// Submit an order to exchange two currencies, but with all the nonsense removed
        /// </summary>
        /// <param name="l2Pk">Loopring Private Key</param>
        /// <param name="apiKey">Current Loopring API Key</param>
        /// <param name="accountId">Wallet Account Id</param>
        /// <param name="sellCurrency">The name of the token you are selling (ETH, LRC, USDT, etc)</param>
        /// <param name="sellAmmount">How much of that token you are selling</param>
        /// <param name="buyCurrency">The name of the token you are buying (ETH, LRC, USDT, etc)</param>
        /// <param name="buyAmmount">How much of that token you are buying</param>        
        /// <param name="orderType">Order types, can be AMM, LIMIT_ORDER, MAKER_ONLY, TAKER_ONLY</param>
        /// <param name="poolAddress">The AMM pool address if order type is AMM</param>
        /// <returns>Returns OrderResult which basically contains the status of your transaction after it was succesfully requested</returns>
        public Task<OrderResult> SubmitOrder(string l2Pk, string apiKey, int accountId,
            string sellCurrency,
            decimal sellAmmount,
            string buyCurrency,
            decimal buyAmmount,
            OrderType orderType,
            string poolAddress = null)
        {
            var tradeChannel = TradeChannel.MIXED;
            if (orderType == OrderType.MAKER_ONLY)
                tradeChannel = TradeChannel.ORDER_BOOK;

            return SubmitOrder(l2Pk, apiKey, accountId,
                new Token() { tokenId = Constants.TokenIDMapper[sellCurrency], volume = (sellAmmount * 1000000000000000000m).ToString("0") },
                new Token() { tokenId = Constants.TokenIDMapper[buyCurrency], volume = (buyAmmount * 1000000000000000000m).ToString("0") },
                false,
                false,
                GetUnixTimestamp() + (int)TimeSpan.FromDays(365).TotalSeconds, // one year
                63,
                null,
                orderType,
                tradeChannel,
                null,
                poolAddress,
                null);
        }

        /// <summary>
        /// Submit an order to exchange two currencies
        /// </summary>
        /// <param name="l2Pk">Loopring Private Key</param>
        /// <param name="apiKey">Current Loopring API Key</param>
        /// <param name="accountId">Wallet Account Id</param>
        /// <param name="sellToken">The token you are selling</param>
        /// <param name="buyToken">The token you are buying</param>
        /// <param name="allOrNone">Whether the order supports partial fills or not.Currently only supports false as a valid value</param>
        /// <param name="fillAmountBOrS">Fill size by buy token or by sell token</param>
        /// <param name="validUntil">Order expiration time, accuracy is in seconds</param>
        /// <param name="maxFeeBips">Maximum order fee that the user can accept, value range (in ten thousandths) 1 ~ 63</param>
        /// <param name="clientOrderId">An arbitrary, client-set unique order identifier, max length is 120 bytes</param>
        /// <param name="orderType">Order types, can be AMM, LIMIT_ORDER, MAKER_ONLY, TAKER_ONLY</param>
        /// <param name="tradeChannel">	Order channel, can be ORDER_BOOK, AMM_POOL, MIXED</param>
        /// <param name="taker">Used by the P2P order which user specify the taker, so far its 0x0000000000000000000000000000000000000000</param>
        /// <param name="poolAddress">The AMM pool address if order type is AMM</param>
        /// <param name="affiliate">An accountID who will recieve a share of the fee of this order</param>
        /// <returns>Returns OrderResult which basically contains the status of your transaction after it was succesfully requested</returns>
        public async Task<OrderResult> SubmitOrder(string l2Pk, string apiKey, int accountId,
            Token sellToken,
            Token buyToken,
            bool allOrNone,
            bool fillAmountBOrS,
            long validUntil,
            int maxFeeBips = 20,
            string clientOrderId = null,
            OrderType? orderType = null,
            TradeChannel? tradeChannel = null,
            string taker = null,
            string poolAddress = null,
            string affiliate = null)

        {
            var request = new ApiSubmitOrderRequest()
            {
                exchange = _exchange,
                accountId = accountId,
                storageId = (await StorageId(apiKey, accountId, sellToken.tokenId)).orderId, // MAYBE? NOT SURE
                sellToken = sellToken,
                buyToken = buyToken,
                allOrNone = allOrNone,
                fillAmountBOrS = fillAmountBOrS,
                validUntil = validUntil,
                maxFeeBips = maxFeeBips,
            };

            if (!string.IsNullOrWhiteSpace(clientOrderId))
                request.clientOrderId = clientOrderId;
            if (orderType.HasValue)
                request.orderType = orderType.Value.ToString();
            if (tradeChannel.HasValue)
                request.tradeChannel = tradeChannel.Value.ToString();
            if (!string.IsNullOrWhiteSpace(taker))
                request.taker = taker;
            if (!string.IsNullOrWhiteSpace(poolAddress))
                request.poolAddress = poolAddress;
            if (!string.IsNullOrWhiteSpace(affiliate))
                request.affiliate = affiliate;

            int MAX_INPUT = 11;
            var poseidonHasher = new Poseidon(MAX_INPUT + 1, 6, 53, "poseidon", 5, _securityTarget: 128);

            BigInteger itaker = string.IsNullOrWhiteSpace(request.taker) ? 0 : BigInteger.Parse(request.taker, System.Globalization.NumberStyles.HexNumber);
            int ifillAmountBOrS = (fillAmountBOrS ? 1 : 0);
            var exchange = BigInteger.Parse(request.exchange.Substring(2, request.exchange.Length - 2), System.Globalization.NumberStyles.HexNumber);

            BigInteger[] inputs = {
                exchange,
                request.storageId,
                request.accountId,
                request.sellToken.tokenId,
                request.buyToken.tokenId,
                BigInteger.Parse(request.sellToken.volume),
                BigInteger.Parse(request.buyToken.volume),
                request.validUntil,
                request.maxFeeBips,
                ifillAmountBOrS,
                itaker
            };

            var signer = new Eddsa(poseidonHasher.CalculatePoseidonHash(inputs), l2Pk);
            var signedMessage = signer.Sign();
            request.eddsaSignature = signedMessage;

            var url = $"{_apiUrl}{Constants.OrderUrl}";
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, url))
            {
                httpRequest.Headers.Add("X-API-KEY", apiKey);

                using (var stringContent = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json"))
                {
                    httpRequest.Content = stringContent;
                    using (var httpResult = await _client.SendAsync(httpRequest))
                    {
                        _ = await ThrowIfHttpFail(httpResult);
                        var resultBody = await httpResult.Content.ReadAsStringAsync();
                        var apiresult = JsonConvert.DeserializeObject<ApiOrderSubmitResult>(resultBody);
                        return new OrderResult(apiresult);
                    }
                }
            }
        }

        /// <summary>
        /// Change the ApiKey associated with the user's account
        /// </summary>
        /// <param name="l2Pk">Loopring Private Key</param>
        /// <param name="apiKey">Current Loopring API Key</param>
        /// <param name="accountId">Wallet Account Id</param>
        /// <returns>The new apiKey as string</returns>
        /// <exception cref="System.Exception">Gets thrown when there's a problem getting info from the Loopring API endpoint</exception>
        public async Task<string> UpdateApiKey(string l2Pk, string apiKey, int accountId)
        {
            string requestBody = "{\"accountId\":" + accountId + "}";
            var signedMessage = EddsaSignUrl(
                l2Pk,
                HttpMethod.Post,
                null,
                requestBody,
                Constants.ApiKeyUrl);

            var url = $"{_apiUrl}{Constants.ApiKeyUrl}";
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, url))
            {
                httpRequest.Headers.Add("X-API-SIG", signedMessage);
                httpRequest.Headers.Add("X-API-KEY", apiKey);
                httpRequest.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                using (var httpResult = await _client.SendAsync(httpRequest))
                {
                    _ = ThrowIfHttpFail(httpResult);
                    var resultBody = await httpResult.Content.ReadAsStringAsync();
                    var apiresult = JsonConvert.DeserializeObject<ApiApiKeyResult>(resultBody);
                    return apiresult.apiKey;
                }
            }
        }

        #endregion
        #region apiKey

        /// <summary>
        /// Fetches the next order id for a given sold token
        /// </summary>
        /// <param name="apiKey">Your Loopring API Key</param>
        /// <param name="accountId">Loopring account identifier</param>
        /// <param name="sellTokenId">The unique identifier of the token which the user wants to sell in the next order.</param>
        /// <param name="maxNext">Return the max of the next available storageId, so any storageId > returned value is avaliable, to help user manage storageId by themselves. for example, if [20, 60, 100] is avaliable, all other ids < 100 is used before, user gets 20 if flag is false (and 60 in next run), but gets 100 if flag is true, so he can use 102, 104 freely</param>
        /// <returns>Returns an object instance of StorageId which contains the next offchainId and orderId</returns>
        /// <exception cref="System.Exception">Gets thrown when there's a problem getting info from the Loopring API endpoint</exception>
        public async Task<StorageId> StorageId(string apiKey, int accountId, int sellTokenId, int maxNext = 0)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new System.Exception("StorageId REQUIRES a valid Loopring wallet apiKey");

            var url = $"{_apiUrl}{Constants.StorageIdUrl}?accountId={accountId}&sellTokenId={sellTokenId}&maxNext={maxNext}";
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Get, url))
            {
                httpRequest.Headers.Add("X-API-KEY", apiKey);
                using (var httpResult = await _client.SendAsync(httpRequest))
                {
                    _ = await ThrowIfHttpFail(httpResult);
                    var resultBody = await httpResult.Content.ReadAsStringAsync();
                    var apiresult = JsonConvert.DeserializeObject<ApiStorageIdResult>(resultBody);
                    return new StorageId()
                    {
                        offchainId = apiresult.offchainId,
                        orderId = apiresult.orderId
                    };
                }
            }
        }

        /// <summary>
        /// Get the details of an order based on order hash.
        /// </summary>
        /// <param name="apiKey">Current Loopring API Key</param>
        /// <param name="accountId">Wallet Account Id</param>
        /// <param name="orderHash">The hash of the worder for which you want details</param>
        /// <returns>OrderDetails object filled with awesome order details</returns>
        /// <exception cref="System.Exception">Gets thrown when there's a problem getting info from the Loopring API endpoint</exception>
        public async Task<OrderDetails> OrderDetails(string apiKey, int accountId, string orderHash)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new System.Exception("StorageId REQUIRES a valid Loopring wallet apiKey");
            if (string.IsNullOrWhiteSpace(orderHash))
                throw new System.Exception("StorageId REQUIRES a valid order hash. Use one of the get order methods to get one");

            var url = $"{_apiUrl}{Constants.OrderUrl}?accountId={accountId}&orderHash={orderHash}";
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Get, url))
            {
                httpRequest.Headers.Add("X-API-KEY", apiKey);
                using (var httpResult = await _client.SendAsync(httpRequest))
                {
                    _ = await ThrowIfHttpFail(httpResult);
                    var resultBody = await httpResult.Content.ReadAsStringAsync();
                    var apiresult = JsonConvert.DeserializeObject<ApiOrderGetResult>(resultBody);
                    return new OrderDetails(apiresult);
                }
            }
        }

        /// <summary>
        /// Get how much fee you need to pay right now to carry out a transaction of a specified type
        /// </summary>
        /// <param name="apiKey">Your Loopring API Key</param>
        /// <param name="accountId">Loopring account identifier</param>
        /// <param name="requestType">Off-chain request type</param>
        /// <param name="tokenSymbol">Required only for withdrawls - The token you wish to withdraw</param>
        /// <param name="amount">Required only for withdrawls - how much of that token you wish to withdraw</param>
        /// <returns>Returns the fee amount</returns>
        /// <exception cref="System.Exception">Gets thrown when there's a problem getting info from the Loopring API endpoint</exception>
        public async Task<OffchainFee> OffchainFee(string apiKey, int accountId, OffChainRequestType requestType, string tokenSymbol, string amount)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new System.Exception("StorageId REQUIRES a valid Loopring wallet apiKey");

            var url = $"{_apiUrl}{Constants.OffchainFeeUrl}?accountId={accountId}&requestType={(int)requestType}&tokenSymbol={tokenSymbol}&amount={amount}";
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Get, url))
            {
                httpRequest.Headers.Add("X-API-KEY", apiKey);
                using (var httpResult = await _client.SendAsync(httpRequest))
                {
                    _ = ThrowIfHttpFail(httpResult);
                    var resultBody = await httpResult.Content.ReadAsStringAsync();
                    var apiresult = JsonConvert.DeserializeObject<ApiOffchainFeeResult>(resultBody);
                    return new OffchainFee()
                    {
                        fees = apiresult.fees,
                        gasPrice = apiresult.gasPrice
                    };
                }

            }
        }

        /// <summary>
        /// Get a list of orders satisfying certain criteria.
        /// </summary>
        /// <param name="apiKey">Your Loopring API Key</param>
        /// <param name="accountId">Loopring account identifier</param>
        /// <param name="market">Trading pair (ex. Trading pair)</param>
        /// <param name="start">Lower bound of order's creation timestamp in millisecond (ex. 1567053142000)</param>
        /// <param name="end">Upper bound of order's creation timestamp in millisecond (ex. 1567053242000)</param>
        /// <param name="side">"BUY" or "SELL"</param>
        /// <param name="statuses">Order statuses to search by</param>
        /// <param name="orderTypes">Order types to search by</param>
        /// <param name="tradeChannels">Trade channels to search by</param>
        /// <param name="limit">How many results per call? Default 50</param>
        /// <param name="offset">How many results to skip? Default 0 </param>
        /// <returns>List of OrderDetails objects containing the searched-for items</returns>
        public async Task<List<OrderDetails>> OrdersDetails(string apiKey, 
            int accountId, 
            string market, 
            long start, 
            long end, 
            Side? side, 
            List<OrderStatus> statuses, 
            List<OrderType> orderTypes, 
            List<TradeChannel> tradeChannels, 
            int limit = 50, 
            int offset = 0)
        {
            var url = $"{_apiUrl}{Constants.OrdersUrl}?accountId={accountId}";
            if (!string.IsNullOrWhiteSpace(market))
                url += $"&market={market}";
            if (start != 0)
                url+= $"&start={start}";
            if (end != 0)
                url += $"&end={end}";
            if(side.HasValue)
                url += $"&side={side.ToString()}";
            if (statuses != null)
                url += $"&status={string.Join(",", statuses.Select(s => s.ToString()))}";
            if(limit!=50)
                url += $"&limit={limit}";
            if(offset!=0)
                url += $"&offset={offset}";
            if (orderTypes != null)
                url += $"&orderTypes={string.Join(",", orderTypes.Select(s => s.ToString()))}";
            if (tradeChannels != null)
                url += $"&tradeChannels={string.Join(",", tradeChannels.Select(s => s.ToString()))}";

            using (var httpRequest = new HttpRequestMessage(HttpMethod.Get, url))
            {
                httpRequest.Headers.Add("X-API-KEY", apiKey);
                using (var httpResult = await _client.SendAsync(httpRequest))
                {
                    _ = await ThrowIfHttpFail(httpResult);
                    var resultBody = await httpResult.Content.ReadAsStringAsync();
                    var apiresult = JsonConvert.DeserializeObject<ApiOrdersGetResult>(resultBody);
                    if (apiresult != null && apiresult.totalNum != 0)
                    {
                        return apiresult.orders.Select(s => new OrderDetails(s)).ToList();
                    }
                    return null;
                }
            }
        }

        #endregion
        #region apiKeyL1L2
        public async Task<Transfer> Transfer(string apiKey, string l2Pk, string l1Pk, TransferRequest request, string memo, string clientId, CounterFactualInfo counterFactualInfo)
        {
            throw new NotImplementedException("Still working on it...");

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new System.Exception("Transfer REQUIRES a valid Loopring wallet apiKey");
            if (string.IsNullOrWhiteSpace(l2Pk))
                throw new System.Exception("Transfer REQUIRES a valid Loopring Wallet Layer 2 Private key");
            if (string.IsNullOrWhiteSpace(l1Pk))
                throw new System.Exception("Transfer REQUIRES a valid Eth Wallet Layer 1 Private key");

            string apiSig = ""; //Need to generate            

            int MAX_INPUT = 13;
            var poseidonHasher = new Poseidon(MAX_INPUT + 1, 6, 53, "poseidon", 5, _securityTarget: 128);
            BigInteger[] inputs = {
                BigInteger.Parse(request.exchange, System.Globalization.NumberStyles.HexNumber),
                (BigInteger)request.payerId,
                (BigInteger)request.payeeId,
                (BigInteger)request.token.tokenId,
                BigInteger.Parse(request.token.volume),
                (BigInteger)request.maxFee.tokenId,
                BigInteger.Parse(request.maxFee.volume),
                BigInteger.Parse(request.payeeAddress, System.Globalization.NumberStyles.HexNumber),
                0,
                0,
                (BigInteger)request.validUnitl,
                (BigInteger)request.storageId
            };

            var apiRequest = request.GetApiTransferRequest(memo, clientId, counterFactualInfo);

            var signer = new Eddsa(poseidonHasher.CalculatePoseidonHash(inputs), l2Pk);
            var signedMessage = signer.Sign(apiRequest);
            apiRequest.eddsaSignature = signedMessage;

            // TODO : Compare Eddsa with python result

            // TODO : Implement ECDSA


            var url = $"{_apiUrl}{Constants.TransferUrl}";
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, url))
            {
                httpRequest.Headers.Add("X-API-KEY", apiKey);
                httpRequest.Headers.Add("X-API-SIG", apiSig);

                using (var stringContent = new StringContent(JsonConvert.SerializeObject(apiRequest), Encoding.UTF8, "application/json"))
                {
                    httpRequest.Content = stringContent;
                    using (var httpResult = await _client.SendAsync(httpRequest))
                    {
                        _ = await ThrowIfHttpFail(httpResult);
                        var resultBody = await httpResult.Content.ReadAsStringAsync();
                        var apiresult = JsonConvert.DeserializeObject<ApiTransferResult>(resultBody);
                        return new Transfer(apiresult);
                    }
                }
            }
        }
        #endregion

        #region private methods
        private string UrlEncodeUpperCase(string stringToEncode)
        {
            var reg = new Regex(@"%[a-f0-9]{2}");
            stringToEncode = HttpUtility.UrlEncode(stringToEncode);
            return reg.Replace(stringToEncode, m => m.Value.ToUpperInvariant());
        }

        private async Task<bool> ThrowIfHttpFail(HttpResponseMessage httpResult)
        {
            if (httpResult.IsSuccessStatusCode)
                return true;
            if (httpResult.Content != null)
            {
                var exString = "Error from Loopring API: " + httpResult.StatusCode.ToString() + " | " + (await httpResult.Content.ReadAsStringAsync());
                throw new System.Exception(exString);
            }
            throw new System.Exception("Error from Loopring API: " + httpResult.StatusCode.ToString());
        }

        private BigInteger CreateSha256Signature(HttpMethod method, List<(string Key, string Value)> queryParams, string postBody, string apiMethod)
        {
            var signatureBase = "";
            var parameterString = "";
            if (method == HttpMethod.Post)
            {
                signatureBase += "POST&";
                parameterString = postBody;
            }
            else if (method == HttpMethod.Get)
            {
                signatureBase += "GET&";
                if (queryParams != null)
                {
                    int i = 0;
                    foreach (var parameter in queryParams)
                    {
                        parameterString += parameter.Key + "=" + parameter.Value;
                        if (i < queryParams.Count - 1)
                            parameterString += "&";
                        i++;
                    }
                }
            }
            else if (method == HttpMethod.Delete)
            {
                signatureBase += "DELETE&";
                if (queryParams != null)
                {
                    int i = 0;
                    foreach (var parameter in queryParams)
                    {
                        parameterString += parameter.Key + "=" + parameter.Value;
                        if (i < queryParams.Count - 1)
                            parameterString += "&";
                        i++;
                    }
                }
            }
            else
                throw new Exception("Http method type not supported");

            signatureBase += UrlEncodeUpperCase(_apiUrl + apiMethod) + "&";
            signatureBase += UrlEncodeUpperCase(parameterString);

            return SHA256Helper.CalculateSHA256HashNumber(signatureBase);
        }

        private string EddsaSignUrl(string l2Pk, HttpMethod method, List<(string Key, string Value)> queryParams, string postBody, string apiMethod)
        {
            var message = CreateSha256Signature(
                method,
                queryParams,
                postBody,
                apiMethod);

            var signer = new Eddsa(message, l2Pk);
            return signer.Sign();
        }
        #endregion

        public void LoadTokenMapper()
        {
            Constants.TokenIDMapper.Add("ETH", 0);
            Constants.TokenIDMapper.Add("LRC", 1);
        }

        public long GetUnixTimestamp() => (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
    }

}
