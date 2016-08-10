﻿/*
 * Jot v1.0
 * License: The MIT License (MIT)
 * Code: https://github.com/jdemeuse1204/Jot
 * Email: james.demeuse@gmail.com
 * Copyright (c) 2016 James Demeuse
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Jot
{
    public class JwtTokenProvider
    {
        #region Constructor
        public JwtTokenProvider(int jwtTimeOutInMinutes, HashAlgorithm hashAlgorithm, bool useGhostClaims = false)
        {
            JwtTimeout = jwtTimeOutInMinutes;
            HashAlgorithm = hashAlgorithm;
            UseGhostClaims = useGhostClaims;
        }

        public JwtTokenProvider()
        {
            var section = _getConfigurationSection();

            // make sure the configuration is valid
            _checkConfigurationIsValid(section);

            JwtTimeout = section.GetTimeOut();
            HashAlgorithm = section.GetHashAlgorithm();
            UseGhostClaims = section.UseGhostClaims();
        }
        #endregion

        #region Events
        public delegate bool OnJtiValidateHandler(Guid jti);

        public event OnJtiValidateHandler OnJtiValidate;



        public delegate Dictionary<string, object> OnGetGhostClaimsHandler();

        public event OnGetGhostClaimsHandler OnGetGhostClaims;



        public delegate bool OnTokenValidateHandler(IJwtToken token);

        public event OnTokenValidateHandler OnTokenValidate;



        public delegate void OnTokenCreateHandler(IJwtToken token);

        public event OnTokenCreateHandler OnCreate;



        public event OnSerializeHandler OnSerialize;

        public delegate string OnSerializeHandler(object toSerialize);



        public event OnDeserializeClaimsHandler OnDeserialize;

        public delegate Dictionary<string, object> OnDeserializeClaimsHandler(string jsonString);



        public event OnHashHandler OnHash;

        public delegate byte[] OnHashHandler(byte[] toEncrypt, string secret);


        #endregion

        #region Properties and Fields
        public readonly int JwtTimeout;

        public readonly HashAlgorithm HashAlgorithm;

        public readonly bool UseGhostClaims;

        private readonly IDictionary<HashAlgorithm, Func<string, byte[], string>> _hashAlgorithms = new Dictionary <HashAlgorithm, Func<string, byte[], string>>
        {
            {
                HashAlgorithm.HS256, (key, value) =>
                {
                    using (var sha = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
                    {
                        return UrlEncode.Base64UrlEncode(sha.ComputeHash(value));
                    }
                }
            },
            {
                HashAlgorithm.HS384, (key, value) =>
                {
                    using (var sha = new HMACSHA384(Encoding.UTF8.GetBytes(key)))
                    {
                        return UrlEncode.Base64UrlEncode(sha.ComputeHash(value));
                    }
                }
            },
            {
                HashAlgorithm.HS512, (key, value) =>
                {
                    using (var sha = new HMACSHA512(Encoding.UTF8.GetBytes(key)))
                    {
                        return UrlEncode.Base64UrlEncode(sha.ComputeHash(value));
                    }
                }
            }
        };
        #endregion

        #region Create
        public IJwtToken Create(Dictionary<string, object> extraHeaders, Dictionary<string, object> claims)
        {
            var token = new JwtToken(JwtTimeout);

            // set and add claims
            foreach (var claim in claims) token.SetClaim(claim.Key, claim.Value);

            // add extra headers
            foreach (var header in extraHeaders) token.SetHeader(header.Key, header.Value);

            if(OnCreate != null) OnCreate(token);

            return token;
        }

        public IJwtToken Create(Dictionary<string, object> claims)
        {
            var token = new JwtToken(JwtTimeout);

            // set and add claims
            foreach (var claim in claims) token.SetClaim(claim.Key, claim.Value);

            if (OnCreate != null) OnCreate(token);

            return token;
        }

        public IJwtToken Create()
        {
            var token = new JwtToken(JwtTimeout);

            if (OnCreate != null) OnCreate(token);

            return token;
        }

        #endregion

        #region Configuration
        private void _checkConfigurationIsValid(JwtAuthConfigurationSection section)
        {
            if (section == null) throw new JwtTokenException("Please configure Jot on the configuration file if you use the parameterless constructor.");

           section.CheckConfigurationIsValid();
        }

        private JwtAuthConfigurationSection _getConfigurationSection()
        {
            return System.Configuration.ConfigurationManager.GetSection(JwtAuthConfigurationSection.SectionName) as JwtAuthConfigurationSection;
        }
        #endregion

        #region Encode
        public string Encode(IJwtToken token)
        {
            var jwt = token as JwtToken;

            if (jwt == null) throw new Exception("Token is not formed correctly");

            var section = _getConfigurationSection();

            return Encode(token, section.GetEncryptionSecret());
        }

        public string Encode(IJwtToken token, string secret)
        {
            var jwt = token as JwtToken;

            if (jwt == null) throw new Exception("Token is not formed correctly");

            // set algorithm in header of jwt
            jwt.SetHeader(JwtDefaultHeaders.ALG, _getEncryptionType());

            // get the headers
            var jwtHeaders = jwt.GetHeaders();
            var jwtClaims = jwt.GetClaims(); // do not include ghost claims

            // serialize header and claims
            var header = _serializeObject(jwtHeaders);
            var claims = _serializeObject(jwtClaims);

            // header and claim bytes
            var headerBytes = Encoding.UTF8.GetBytes(header);
            var claimBytes = Encoding.UTF8.GetBytes(claims);

            //  encoded segments
            var headerSegment = UrlEncode.Base64UrlEncode(headerBytes);
            var claimSegment = UrlEncode.Base64UrlEncode(claimBytes);

            // sign the token
            var getSignedSignature = _getEncrytedSignature(jwt, secret);

            // return final result
            return string.Concat(headerSegment, ".", claimSegment, ".", getSignedSignature);
        }

        private string _getEncryptionType()
        {
            var section = _getConfigurationSection();

            if (section == null) return HashAlgorithm.ToString();

            if (section.AnonymousAlgorithmInHeader()) return "Anonymous";

            if (OnHash != null) return "Custom";

            return HashAlgorithm.ToString();
        }

        #endregion

        #region Other
        private Dictionary<string, object> _getClaimsWithGhostClaims(JwtToken jwt)
        {
            if (OnGetGhostClaims == null) throw new JwtTokenException("Ghost claims are being used, but OnGetGhostClaims is null.");

            var ghostClaims = OnGetGhostClaims();

            if (ghostClaims == null || ghostClaims.Count == 0) throw new JwtTokenException("Ghost claims cannot be null or blank.");

            var jwtClaims = jwt.GetClaims();

            var result = jwtClaims.ToDictionary(claim => claim.Key, claim => claim.Value);

            foreach (var ghostClaim in ghostClaims) result.Add(ghostClaim.Key, ghostClaim.Value);

            return result;
        }

        private string _getEncrytedSignature(JwtToken jwt, string secret)
        {
            // get the headers
            var jwtHeaders = jwt.GetHeaders();
            var jwtClaims = jwt.GetClaims(); // do not include ghost claims

            // serialize header and claims
            var header = _serializeObject(jwtHeaders);
            var claims = _serializeObject(jwtClaims);
            var signatureClaims = UseGhostClaims ? _serializeObject(_getClaimsWithGhostClaims(jwt)) : claims;

            // header and claim bytes
            var headerBytes = Encoding.UTF8.GetBytes(header);
            var signatureClaimBytes = Encoding.UTF8.GetBytes(signatureClaims);

            //  encoded segments
            var headerSegment = UrlEncode.Base64UrlEncode(headerBytes);
            var signatureClaimsSegment = UrlEncode.Base64UrlEncode(signatureClaimBytes);

            // sign the token
            var unsignedSignature = string.Concat(headerSegment, ".", signatureClaimsSegment);
            var signatureBytes = Encoding.UTF8.GetBytes(unsignedSignature);

            // return encoded signature that must be signed
            return _hash(secret, signatureBytes);
        }

        private string _hash(string secret, byte[] messageBytes)
        {
            return OnHash != null ? UrlEncode.Base64UrlEncode(OnHash(messageBytes, secret)) : _hashAlgorithms[HashAlgorithm](secret, messageBytes);
        }

        #endregion

        #region Decode

        public IJwtToken Decode(string encodedToken)
        {
            var parts = encodedToken.Split('.');

            if (parts.Count() != 3) throw new JwtTokenException("Token does not consist of three parts");

            // parts
            var header = parts[0];
            var claims = parts[1];

            // get bytes of parts
            var headerBytes = UrlEncode.Base64UrlDecode(header);
            var claimBytes = UrlEncode.Base64UrlDecode(claims);

            // get segments
            var headerSegment = Encoding.UTF8.GetString(headerBytes);
            var claimSegment = Encoding.UTF8.GetString(claimBytes);

            // decode claims to object
            var claimsObject = _decodeObject(claimSegment);
            var headerObject = _decodeObject(headerSegment);

            return new JwtToken(headerObject, claimsObject);
        }
        #endregion

        #region Object Serialization
        public Dictionary<string, object> _decodeObject(string jsonString)
        {
            return OnDeserialize != null ? OnDeserialize(jsonString) : Serializer.ToObject<Dictionary<string, object>>(jsonString);
        }

        private string _serializeObject(object entity)
        {
            return OnSerialize != null ? OnSerialize(entity) : Serializer.ToJSON(entity);
        }
        #endregion

        #region Refresh Token

        public IJwtToken Refresh(string encodedToken)
        {
            var token = Decode(encodedToken);

            return Refresh(token);
        }

        public IJwtToken Refresh(IJwtToken token)
        {
            var jwt = token as JwtToken;

            if (jwt == null) throw new Exception("Token is not formed correctly");

            // refresh the expiration date
            jwt.SetClaim(JwtDefaultClaims.EXP, UnixDateServices.GetUnixTimestamp(JwtTimeout));

            return token;
        }

        #endregion

        #region Validation

        public TokenValidationResult Validate(string encodedToken)
        {
            return Validate(encodedToken, new JwtValidationContainer());
        }

        public TokenValidationResult Validate(string encodedToken, JwtValidationContainer validationContainer)
        {
            var section = _getConfigurationSection();

            return Validate(encodedToken, section.GetEncryptionSecret(), validationContainer);
        }

        public TokenValidationResult Validate(string encodedToken, string secret)
        {
            return Validate(encodedToken, secret, new JwtValidationContainer());
        }

        public TokenValidationResult Validate(string encodedToken, string secret, JwtValidationContainer validationContainer)
        {
            var token = Decode(encodedToken);
            var jwt = token as JwtToken;

            if (jwt == null) return TokenValidationResult.TokenNotCorrectlyFormed;

            // if the split does not produce 3 parts the decode part will catch it
            var signatureFromToken = encodedToken.Split('.')[2];

            // re create signature to check for a match
            var recreatedSignedSignature = _getEncrytedSignature(jwt, secret);

            if (!string.Equals(signatureFromToken, recreatedSignedSignature)) return TokenValidationResult.SignatureNotValid;

            var jti = jwt.GetClaim<Guid>(JwtDefaultClaims.JTI);
            var nbf = jwt.GetClaim<double>(JwtDefaultClaims.NBF);
            var exp = jwt.GetClaim<double>(JwtDefaultClaims.EXP);

            // check iat, cannot be 0

            // check nbf
            var currentUnixTime = UnixDateServices.GetUnixTimestamp();
            var isNbfValid = true;

            if (validationContainer.CheckNfb)
            {
                // Not Before should not be before current time
                isNbfValid = nbf <= currentUnixTime;
            }

            if (!isNbfValid) return TokenValidationResult.NotBeforeFailed;

            // check expiration date
            if (currentUnixTime >= exp) return TokenValidationResult.TokenExpired;

            // check the custom handler after everything.  
            // potentially saves in processing time if the claim is expired
            if (OnTokenValidate != null && !OnTokenValidate(jwt)) return TokenValidationResult.OnTokenValidateFailed;

            if (OnJtiValidate != null && !OnJtiValidate(jti)) return TokenValidationResult.OnJtiValidateFailed;

            if (!validationContainer.Any()) return TokenValidationResult.Passed;

            // perform custom checks
            foreach (var item in validationContainer)
            {
                var claim = jwt.GetClaim(item.Key);

                if (!object.Equals(claim, item.Value)) return TokenValidationResult.CustomCheckFailed;
            }

            return TokenValidationResult.Passed;
        }
        #endregion

        #region Serialization

        private static class Serializer
        {
            public static string ToJSON(object toSerialize)
            {
                return new JavaScriptSerializer().Serialize(toSerialize);
            }

            public static T ToObject<T>(string json) where T : class
            {
                return new JavaScriptSerializer().Deserialize(json, typeof(T)) as T;
            }
        }

        #endregion

        #region Token

        //https://stormpath.com/blog/jwt-the-right-way
        //https://tools.ietf.org/html/draft-ietf-oauth-json-web-token-32#section-4.1.4
        public class JwtToken : IJwtToken
        {
            #region Properties And Fields

            private readonly Dictionary<string, object> _claims;

            private readonly Dictionary<string, object> _header;

            #endregion

            #region Constructor

            public JwtToken(int jwtTimeOut)
            {
                _claims = new Dictionary<string, object>
                {
                    {JwtDefaultClaims.IAT, UnixDateServices.GetUnixTimestamp()},
                    {JwtDefaultClaims.EXP, UnixDateServices.GetUnixTimestamp(jwtTimeOut)},
                    {JwtDefaultClaims.ROL, ""},
                    {JwtDefaultClaims.JTI, Guid.NewGuid()},
                    {JwtDefaultClaims.ISS, ""},
                    {JwtDefaultClaims.AUD, ""},
                    {JwtDefaultClaims.NBF, UnixDateServices.GetUnixTimestamp()},
                    {JwtDefaultClaims.SUB, ""},
                    {JwtDefaultClaims.USR, ""}
                };

                _header = new Dictionary<string, object>
                {
                    {JwtDefaultHeaders.ALG, ""},
                    {JwtDefaultHeaders.TYP, "JWT"}
                };
            }

            public JwtToken(Dictionary<string, object> header, Dictionary<string, object> claims)
            {
                _claims = claims;
                _header = header;
            }

            #endregion
            public void SetClaim(string claimKey, object value)
            {
                if (!_claims.ContainsKey(claimKey))
                {
                    _claims.Add(claimKey, value);
                    return;
                }

                _claims[claimKey] = value;
            }

            public bool ClaimExists(string claimKey)
            {
                return _claims.ContainsKey(claimKey);
            }

            public T GetHeader<T>(string headerKey)
            {
                return typeof(T) == typeof(Guid) ? (T) (dynamic) Guid.Parse(_header[headerKey].ToString()) : (T) Convert.ChangeType(_header[headerKey], typeof(T));
            }

            public object GetHeader(string headerKey)
            {
                return _header[headerKey];
            }

            public void SetHeader(string headerKey, object value)
            {
                if (!_header.ContainsKey(headerKey))
                {
                    _header.Add(headerKey, value);
                    return;
                }

                _header[headerKey] = value;
            }

            public bool HeaderExists(string headerKey)
            {
                return _header.ContainsKey(headerKey);
            }

            public T GetClaim<T>(string claimKey)
            {
                return typeof(T) == typeof(Guid) ? (T) (dynamic) Guid.Parse(_claims[claimKey].ToString()) : (T) Convert.ChangeType(_claims[claimKey], typeof(T));
            }

            public object GetClaim(string claimKey)
            {
                return _claims[claimKey];
            }

            public Dictionary<string, object> GetHeaders()
            {
                return _header;
            }

            public Dictionary<string, object> GetClaims()
            {
                return _claims;
            }
        }

        #endregion

        #region Encoding

        private static class UrlEncode
        {
            // From https://tools.ietf.org/html/draft-ietf-jose-json-web-signature-08#appendix-C
            public static string Base64UrlEncode(byte[] payload)
            {
                var s = Convert.ToBase64String(payload); // Regular base64 encoder
                s = s.Split('=')[0]; // Remove any trailing '='s
                s = s.Replace('+', '-'); // 62nd char of encoding
                s = s.Replace('/', '_'); // 63rd char of encoding
                return s;
            }

            public static byte[] Base64UrlDecode(string payload)
            {
                var s = payload;
                s = s.Replace('-', '+'); // 62nd char of encoding
                s = s.Replace('_', '/'); // 63rd char of encoding
                switch (s.Length%4) // Pad with trailing '='s
                {
                    case 0:
                        break; // No pad chars in this case
                    case 2:
                        s += "==";
                        break; // Two pad chars
                    case 3:
                        s += "=";
                        break; // One pad char
                    default:
                        throw new System.Exception("Illegal base64url string!");
                }

                return Convert.FromBase64String(s); // Standard base64 decoder
            }
        }

        #endregion
        
        #region Date Services 

        private static class UnixDateServices
        {
            public static double GetUnixTimestamp(double jwtAuthorizationTimeOutInMinutes)
            {
                var millisecondsTimeOut = ((jwtAuthorizationTimeOutInMinutes * 60)*1000);

                return Math.Round(GetUnixTimestamp() + millisecondsTimeOut);
            }

            private static DateTime _unixEpoch()
            {
                return new DateTime(1970, 1, 1).ToLocalTime();
            }

            public static double GetUnixTimestamp()
            {
                return Math.Round(DateTime.UtcNow.Subtract(_unixEpoch()).TotalSeconds);
            }
        }

        #endregion
    }
}
