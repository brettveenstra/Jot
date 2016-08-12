﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Jot.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Jot.Tests
{
    [TestClass]
    public class JotGeneralTests
    {
        [TestMethod]
        public void CreateClaimWithNoPayload()
        {
            var jot = new JotProvider();

            var token = jot.Create();

            Assert.IsNotNull(token);
        }

        [TestMethod]
        public void CreateClaimWithPayload()
        {
            var jot = new JotProvider();

            var payload = new Dictionary<string, object>
            {
                {"iat", 0},
                {"exp", 0},
                {"rol", "sdf"},
                {"jti", ""},
                {"iss", ""},
                {"aud", ""},
                {"nbf", ""},
                {"sub", ""},
                {"usr", ""}
            };

            var token = jot.Create(payload);

            Assert.IsNotNull(token);
        }

        [TestMethod]
        public void CheckDefaultCreationValues()
        {
            var jot = new JotProvider();

            var token = jot.Create();

            var exp = token.GetClaim<double>("exp");
            var iat = token.GetClaim<double>("iat");
            var jti = token.GetClaim<Guid>("jti");
            var nbf = token.GetClaim<double>("nbf");

            Assert.IsTrue(exp > 0 && iat > 0 && nbf > 0 && jti != Guid.Empty);
        }

        [TestMethod]
        public void CreateClaimWithPayloadAndMakeSureValuesAreSet()
        {
            var jot = new JotProvider();

            var payload = new Dictionary<string, object>
            {
                {"iat", ""},
                {"exp", ""},
                {"rol", "Test"},
                {"jti", Guid.Empty},
                {"iss", "Test"},
                {"aud", ""},
                {"nbf", ""},
                {"sub", ""},
                {"usr", ""}
            };

            var token = jot.Create(payload);

            var rol = token.GetClaim<string>("rol");
            var jti = token.GetClaim<Guid>("jti");
            var iss = token.GetClaim<string>("iss");

            Assert.IsTrue(string.Equals(rol, "Test") && string.Equals(iss, "Test") && jti == Guid.Empty);
        }

        [TestMethod]
        public void MakeSureClaimIsEncryptedCorrectly()
        {
            var jot = new JotProvider();

            var token = jot.Create();

            var jwt = jot.Encode(token);

            Assert.IsTrue(jwt.Split('.').Count() == 3);
        }

        [TestMethod]
        public void CheckNbf_AddTimeToSetTheNotBeforeToALaterDate()
        {
            var jot = new JotProvider();

            var payload = new Dictionary<string, object>
            {
                {"iat", UnixDateServices.GetUnixTimestamp()},
                {"exp", UnixDateServices.GetUnixTimestamp(30)},
                {"rol", "Test"},
                {"jti", Guid.Empty},
                {"iss", "Test"},
                {"aud", ""},
                {"nbf", (UnixDateServices.GetUnixTimestamp(0) + 10000)},
                {"sub", ""},
                {"usr", ""}
            };

            var token = jot.Create(payload);

            var jwt = jot.Encode(token);

            var isValid = jot.Validate(jwt);

            Assert.IsTrue(isValid == TokenValidationResult.NotBeforeFailed);
        }

        [TestMethod]
        public void CheckNbf_MakeSureItWorksOnItsOwn()
        {
            var jot = new JotProvider();

            var payload = new Dictionary<string, object>
            {
                {"iat", UnixDateServices.GetUnixTimestamp()},
                {"exp", UnixDateServices.GetUnixTimestamp(30)},
                {"rol", "Test"},
                {"jti", Guid.Empty},
                {"iss", "Test"},
                {"aud", ""},
                {"sub", ""},
                {"usr", ""}
            };

            var token = jot.Create(payload);

            var jwt = jot.Encode(token);

            var validationResult = jot.Validate(jwt);

            Assert.IsTrue(validationResult == TokenValidationResult.Passed);
        }

        [TestMethod]
        public void MakeSureExpClaimIsWorking()
        {
            var jot = new JotProvider();

            var token = jot.Create();

            var jwt = jot.Encode(token);

            Thread.Sleep(61000);

            var validationResult = jot.Validate(jwt);

            Assert.IsTrue(validationResult == TokenValidationResult.TokenExpired);
        }

        [TestMethod]
        public void MakeSureIatClaimIsWorking()
        {
            var jot = new JotProvider();

            var token = jot.Create();

            token.SetClaim(JotDefaultClaims.IAT, 0);

            var jwt = jot.Encode(token);

            var validationResult = jot.Validate(jwt);

            Assert.IsTrue(validationResult == TokenValidationResult.CreatedTimeCheckFailed);
        }

        [TestMethod]
        public void MakeSureIatClaimIsWorking_SetIatToFutureDate()
        {
            var jot = new JotProvider();

            var token = jot.Create();

            token.SetClaim(JotDefaultClaims.IAT, UnixDateServices.GetUnixTimestamp(600));

            var jwt = jot.Encode(token);

            var validationResult = jot.Validate(jwt);

            Assert.IsTrue(validationResult == TokenValidationResult.CreatedTimeCheckFailed);
        }

        [TestMethod]
        public void MakeSureIatClaimIsWorking_SetIatToFutureDate_Skip()
        {
            var jot = new JotProvider();

            var token = jot.Create();

            token.SetClaim(JotDefaultClaims.IAT, UnixDateServices.GetUnixTimestamp(600));

            var validationContainer = new JotValidationContainer();

            validationContainer.SkipClaimVerification(JotDefaultClaims.IAT);

            var jwt = jot.Encode(token);

            var validationResult = jot.Validate(jwt, validationContainer);

            Assert.IsTrue(validationResult == TokenValidationResult.Passed);
        }
    }
}