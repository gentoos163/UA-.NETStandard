﻿/* ========================================================================
 * Copyright (c) 2005-2017 The OPC Foundation, Inc. All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/

using Opc.Ua.Configuration;
using Opc.Ua.Gds.Server.Database;
using System;
using System.Threading.Tasks;


namespace Opc.Ua.Gds.Server.Test
{
    public class ApplicationMessageDlg : IApplicationMessageDlg
    {
        private string message = string.Empty;
        private bool ask = false;

        public override void Message(string text, bool ask)
        {
            this.message = text;
            this.ask = ask;
        }

        public override async Task<bool> ShowAsync()
        {
            if (ask)
            {
                message += " (y/n, default y): ";
                Console.Write(message);
            }
            else
            {
                Console.WriteLine(message);
            }
            if (ask)
            {
                try
                {
                    ConsoleKeyInfo result = Console.ReadKey();
                    Console.WriteLine();
                    return await Task.FromResult((result.KeyChar == 'y') || (result.KeyChar == 'Y') || (result.KeyChar == '\r'));
                }
                catch
                {
                    // intentionally fall through
                }
            }
            return await Task.FromResult(true);
        }
    }

    public class GlobalDiscoveryTestServer
    {
        GlobalDiscoveryServer m_server;
        static bool autoAccept = false;

        public GlobalDiscoveryServer Server { get { return m_server; } }

        public GlobalDiscoveryTestServer(bool _autoAccept)
        {
            autoAccept = _autoAccept;
        }

        public async Task StartServer()
        {
            ApplicationInstance.MessageDlg = new ApplicationMessageDlg();
            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "Global Discovery Server",
                ApplicationType = ApplicationType.Server,
                ConfigSectionName = "Opc.Ua.GlobalDiscoveryTestServer"
            };

            // load the application configuration.
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(false);

            // check the application certificate.
            bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, 0);
            if (!haveAppCertificate)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            if (!config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }

            // get the DatabaseStorePath configuration parameter.
            GlobalDiscoveryServerConfiguration gdsConfiguration = config.ParseExtension<GlobalDiscoveryServerConfiguration>();
            string databaseStorePath = gdsConfiguration.DatabaseStorePath;

            // start the server.
            m_server = new GlobalDiscoveryServer(JsonApplicationsDatabase.Load(databaseStorePath));
            await application.Start(m_server);
        }

        public void StopServer()
        { 
            if (m_server != null)
            {
                Console.WriteLine("Server stopped. Waiting for exit...");

                using (GlobalDiscoveryServer server = m_server)
                {
                    // Stop status thread
                    m_server = null;
                    // Stop server and dispose
                    server.Stop();
                }
            }

        }

        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                e.Accept = autoAccept;
                if (autoAccept)
                {
                    Console.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
                }
                else
                {
                    Console.WriteLine("Rejected Certificate: {0}", e.Certificate.Subject);
                }
            }
        }


    }
}