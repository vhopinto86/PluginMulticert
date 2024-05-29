using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;

namespace MultiCertXmlSign
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine();
                Console.WriteLine(@"MultiCertSign.exe ""sign_xml"" ""PRT.505767457-PRT.504036874-51f49bee-00b9-40c4-8726-9bd0aa5e045c"" ""G3stw4r3_2016G3stw4r3_2016"" ""fatura.xml"" ""FT 2021/5"" ""Porto"" ""vhopinto86@gmail.com""");
                Console.WriteLine();
                Console.WriteLine(@"MultiCertSign.exe ""operationid_by_iud"" ""PRT.505767457-PRT.504036874-51f49bee-00b9-40c4-8726-9bd0aa5e045c"" ""G3stw4r3_2016G3stw4r3_2016"" ""FT 2021/5""");
                Console.WriteLine();
                Console.WriteLine(@"MultiCertSign.exe ""xml_by_operationid"" ""PRT.505767457-PRT.504036874-51f49bee-00b9-40c4-8726-9bd0aa5e045c"" ""G3stw4r3_2016G3stw4r3_2016"" ""7498""");

                return;
            }

            string mode = args[0];
            string username = args[1];
            string password = args[2];


            if (mode == "sign_xml")
            {
                if ((args.Length == 6 || args.Length == 7) == false)
                {
                    Console.WriteLine("Número de argumentos inválido.");

                    return;
                }

                string path = args[3];
                string iud = args[4].Replace(" ", "_").Replace("/", "_");
                string localidade = args[5];
                string receiver_email = args.Length > 6 ? args[6] : null;

                string token = GetToken(username, password);

                if (!string.IsNullOrEmpty(token))
                {
                    Sign(token, path, iud, receiver_email);
                }
            }
            else if (mode == "operationid_by_iud")
            {
                if (args.Length != 4)
                {
                    Console.WriteLine("Número de argumentos inválido.");

                    return;
                }

                string iud = args[3].Replace(" ", "_").Replace("/", "_");

                string token = GetToken(username, password);

                if (!string.IsNullOrEmpty(token))
                {
                    GetOperationID(token, iud);
                }
            }
            else if (mode == "xml_by_operationid")
            {
                if (args.Length != 4)
                {
                    Console.WriteLine("Número de argumentos inválido.");

                    return;
                }

                string operationid = args[3];

                string token = GetToken(username, password);

                if (!string.IsNullOrEmpty(token))
                {
                    GetPdfByOperationID(token, operationid);
                }
            }
            else
            {
                Console.WriteLine("Comando inválido.");
            }

            Console.ReadKey();
        }

        public static string GetToken(string username, string password)
        {
            string token = null;

            try
            {
                var authorization_bytes = System.Text.Encoding.UTF8.GetBytes(username + ":" + password);
                string authorization = System.Convert.ToBase64String(authorization_bytes);

                string service_url = "https://staging.must.digital/oauth2/authorization-server/oauth/token";

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(service_url);


                WebHeaderCollection headers = new WebHeaderCollection();

                headers.Add("Authorization", "Basic " + authorization);


                httpWebRequest.Headers = headers;
                httpWebRequest.ContentType = "application/x-www-form-urlencoded";
                httpWebRequest.Accept = "application/json";
                httpWebRequest.Method = "POST";

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    NameValueCollection outgoingQueryString = HttpUtility.ParseQueryString(String.Empty);
                    outgoingQueryString.Add("grant_type", "client_credentials");
                    string postdata = outgoingQueryString.ToString();

                    string json = new JavaScriptSerializer().Serialize(new
                    {
                        grant_type = "client_credentials"
                    });

                    streamWriter.Write(postdata);
                    streamWriter.Flush();
                    streamWriter.Close();
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();

                    //Console.WriteLine("\n" + result.ToString() + "\n");

                    JavaScriptSerializer json_serializer = new JavaScriptSerializer();

                    Dictionary<string, Object> data = json_serializer.DeserializeObject(result) as Dictionary<string, Object>;

                    token = data["access_token"].ToString();

                    //Console.WriteLine(token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return token;
        }

        public static void Sign(string token, string path, string iud, string receiver_email)
        {
            try
            {
                Byte[] bytes = File.ReadAllBytes(path);
                string file = Convert.ToBase64String(bytes);

                string service_url = "https://staging.must.digital/signstash/einvoice-integration-ws/api/v0/document/sign/base64/";

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(service_url);

                WebHeaderCollection headers = new WebHeaderCollection();
                headers.Add("Authorization", "bearer " + token);
                httpWebRequest.Headers = headers;

                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string payload;

                    if (!string.IsNullOrEmpty(receiver_email))
                    {

                        payload = new JavaScriptSerializer().Serialize(new
                        {
                            description = string.Format("Documento Eletrónico {0}", iud),
                            documents = new[] {
                            new {
                                documentRequest = new
                                {
                                    base64Content = file,
                                    contentType = "text/xml",
                                    externalReference = iud,
                                    name = Path.GetFileName(path)
                                },
                                signatureInfo = new
                                {
                                    //advancedSignatureInfo = new
                                    //{
                                    //    canonicalTransform = "EXCLUSIVE_WITH_COMMENTS",
                                    //    signatureLocation = "SignatureInformation",
                                    //    signatureTransform = "ENVELOPED"
                                    //},
                                    applyTimestamp = false,
                                    signatureType = "XAdES"
                                }
                            }
                        },
                            externalReference = iud,
                            preservationInfo = new
                            {
                                alias = Path.GetFileName(path),
                                deliveryInfo = new
                                {
                                    attachDocuments = true,
                                    personReceiver = new[] {
                                        new {
                                            deliveryStatus = "SENT",
                                            email = receiver_email
                                        }
                                    }
                                },
                                preserveContent = false
                            },
                            returnSignedContent = true
                        });
                    }
                    else
                    {
                        payload = new JavaScriptSerializer().Serialize(new
                        {
                            description = string.Format("Documento Eletrónico {0}", iud),
                            documents = new[] {
                            new {
                                documentRequest = new
                                {
                                    base64Content = file,
                                    contentType = "text/xml",
                                    externalReference = iud,
                                    name = Path.GetFileName(path)
                                },
                                signatureInfo = new
                                {
                                    //advancedSignatureInfo = new
                                    //{
                                    //    canonicalTransform = "EXCLUSIVE_WITH_COMMENTS",
                                    //    signatureLocation = "SignatureInformation",
                                    //    signatureTransform = "ENVELOPED"
                                    //},
                                    applyTimestamp = false,
                                    signatureType = "XAdES"
                                }
                            }
                        },
                            externalReference = iud,
                            returnSignedContent = true
                        });
                    }


                    streamWriter.Write(payload);
                    streamWriter.Flush();
                    streamWriter.Close();
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();

                    Console.WriteLine(result.ToString());

                    JavaScriptSerializer json_serializer = new JavaScriptSerializer();

                    Dictionary<string, Object> data = json_serializer.DeserializeObject(result) as Dictionary<string, Object>;

                    if (data["status"].ToString() == "EXECUTED")
                    {
                        string id = data["id"].ToString();

                        string document_list = new JavaScriptSerializer().Serialize(data["documentList"]);

                        Object[] document_list_values = new JavaScriptSerializer().Deserialize<Object[]>(document_list);

                        Dictionary<string, Object> document = document_list_values[0] as Dictionary<string, Object>;

                        byte[] xml_bytes = Convert.FromBase64String(document["signedContent"].ToString());
                        File.WriteAllBytes(Path.GetFileNameWithoutExtension(path) + "_signed.xml", xml_bytes);

                        Console.WriteLine(id);
                        Console.WriteLine(Path.GetFileNameWithoutExtension(path) + "_signed.xml");
                    }
                }
            }
            catch (WebException ex)
            {
                using (var stream = ex.Response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    Console.WriteLine(reader.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void GetOperationID(string token, string iud)
        {
            try
            {
                string service_url = string.Format("https://staging.must.digital/signstash/einvoice-integration-ws/api/v0/operation/id/withDocumentExternalReference/{0}", iud);

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(service_url);

                WebHeaderCollection headers = new WebHeaderCollection();
                headers.Add("Authorization", "bearer " + token);
                httpWebRequest.Headers = headers;
                httpWebRequest.Accept = "application/json";
                httpWebRequest.Method = "GET";

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();

                    JavaScriptSerializer json_serializer = new JavaScriptSerializer();

                    Dictionary<string, Object> json_response = json_serializer.DeserializeObject(result) as Dictionary<string, Object>;

                    Console.WriteLine(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void GetPdfByOperationID(string token, string operationid)
        {
            string service_url = string.Format("https://staging.must.digital/signstash/einvoice-integration-ws/api/v0/operation/sign/detail/{0}/contentInResponse/{1}", operationid, true);

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(service_url);

            WebHeaderCollection headers = new WebHeaderCollection();
            headers.Add("Authorization", "bearer " + token);
            httpWebRequest.Headers = headers;
            httpWebRequest.Accept = "application/json";
            httpWebRequest.Method = "GET";

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();

                JavaScriptSerializer json_serializer = new JavaScriptSerializer();

                Dictionary<string, Object> data = json_serializer.DeserializeObject(result) as Dictionary<string, Object>;

                //Console.WriteLine(result);

                if (data["status"].ToString() == "EXECUTED")
                {
                    string document_list = new JavaScriptSerializer().Serialize(data["documentList"]);

                    Object[] document_list_values = new JavaScriptSerializer().Deserialize<Object[]>(document_list);

                    Dictionary<string, Object> document = document_list_values[0] as Dictionary<string, Object>;

                    byte[] xml_bytes = Convert.FromBase64String(document["signedContent"].ToString());
                    File.WriteAllBytes(document["name"].ToString().Replace(".xml", "") + "_signed.xml", xml_bytes);

                    Console.WriteLine(document["name"].ToString().Replace(".xml", "") + "_signed.xml");
                }
            }
        }
    }
}
