using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using log4net;

namespace TwitterAnalyzer
{
    public class Service : IHttpHandler
    {
        private ErrorHandler errHandler;

        #region IHttpHandler Members

        public bool IsReusable
        {
            get { throw new NotImplementedException(); }
        }

        public void ProcessRequest(HttpContext context)
        {
            ILog logger = LogManager.GetLogger(GetType().FullName);


            try
            {
                logger.Info(string.Format("Request HttpMethod={0}", context.Request.HttpMethod));

                string url = Convert.ToString(context.Request.Url);
                logger.Info(string.Format("Request Url={0}", url));

                errHandler = new ErrorHandler();

                //Handling CRUD
                switch (context.Request.HttpMethod)
                {
                    case "GET":
                        //Perform READ Operation                   
                        READ(context);
                        break;
                    case "POST":
                        //Perform CREATE Operation
                        CREATE(context);
                        break;
                    case "PUT":
                        //Perform UPDATE Operation
                        UPDATE(context);
                        break;
                    case "DELETE":
                        //Perform DELETE Operation
                        DELETE(context);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception exc)
            {
                logger.Error("Failed processing HTTP request.", exc);

                errHandler.ErrorMessage = exc.Message;
                context.Response.Write(errHandler.ErrorMessage);
            }
        }

        #endregion

        #region CRUD Functions
        /// <summary>
        /// CREATE function
        /// </summary>
        /// <param name="context"></param>
        private void CREATE(HttpContext context)
        {
            ILog logger = LogManager.GetLogger(GetType().FullName);

            try
            {
                // HTTP POST sends name/value pairs to a web server
                // data is sent in message body

                // This Post task handles cookies and remembers them across calls. 
                // This means that you can post to a login form, receive authentication cookies, 
                // then subsequent posts will automatically pass the correct cookies. 
                // The cookies are stored in memory only, they are not written to disk and 
                // will cease to exist upon completion of the build.

                // The message body is posted as bytes. 
                logger.Info(string.Format("Request ContentLength={0}", context.Request.ContentLength));
                byte[] postData = context.Request.BinaryRead(context.Request.ContentLength);
                if (postData == null)
                {
                    WriteResponse("FAIL");
                }
                else
                {
                    WriteResponse("OK");
                    //Convert the bytes to string using Encoding class
                    string xmlContent = Encoding.UTF8.GetString(postData);
                    logger.Info(string.Format("Request ContentText={0}", xmlContent));
                }
            }
            catch (HttpException hexc)
            {
                logger.Error("Failed processing POST request.", hexc);

                WriteResponse(string.Format("Error in CREATE: {0}", hexc.Message));
                errHandler.ErrorMessage = hexc.Message;
            }
            catch (Exception exc)
            {
                logger.Error("Failed processing POST request.", exc);

                WriteResponse(string.Format("Error in CREATE: {0}", exc.Message));
                errHandler.ErrorMessage = exc.Message;
            }
        }

        /// <summary>
        /// GET function
        /// </summary>
        /// <param name="context"></param>
        private void READ(HttpContext context)
        {
            ILog logger = LogManager.GetLogger(GetType().FullName);

            //HTTP Request - http://localhost/TwitterAnalyzer/sentiment?text={text} 
            
            try
            {
                string data = context.Request["text"];
                if (string.IsNullOrEmpty(data))
                {
                    logger.ErrorFormat("READ sentiment, data is null or empty");
                    WriteResponse("FAIL");
                }
                else
                {
                    string text = Uri.UnescapeDataString(data);
                    logger.InfoFormat("READ sentiment, text={0}", text);

                    string result = TwitterAnalyzer.ServiceHelper.BuildResult(text, logger);
                    WriteResponse(result);                    
                }
            }
            catch (HttpException hexc)
            {
                logger.Error("Failed processing READ request.", hexc);

                WriteResponse(string.Format("Error in READ: {0}", hexc.Message));
                errHandler.ErrorMessage = hexc.Message;
            }
            catch (Exception exc)
            {
                logger.Error("Failed processing READ request.", exc);

                WriteResponse(string.Format("Error in READ: {0}", exc.Message));
                errHandler.ErrorMessage = exc.Message;
            }
        }
        /// <summary>
        /// UPDATE function
        /// </summary>
        /// <param name="context"></param>
        private void UPDATE(HttpContext context)
        {
            ILog logger = LogManager.GetLogger(GetType().FullName);

            try
            {
                WriteResponse("OK");
            }
            catch (HttpException hexc)
            {
                logger.Error("Failed processing UPDATE request.", hexc);

                WriteResponse(string.Format("Error in UPDATE: {0}", hexc.Message));
                errHandler.ErrorMessage = hexc.Message;
            }
            catch (Exception exc)
            {
                logger.Error("Failed processing UPDATE request.", exc);

                WriteResponse(string.Format("Error in UPDATE: {0}", exc.Message));
                errHandler.ErrorMessage = exc.Message;
            }
        }
        /// DELETE function
        /// </summary>
        /// <param name="context"></param>
        private void DELETE(HttpContext context)
        {
            ILog logger = LogManager.GetLogger(GetType().FullName);

            try
            {
                WriteResponse("OK");
            }
            catch (HttpException hexc)
            {
                logger.Error("Failed processing DELETE request.", hexc);

                WriteResponse(string.Format("Error in DELETE: {0}", hexc.Message));
                errHandler.ErrorMessage = hexc.Message;
            }
            catch (Exception exc)
            {
                logger.Error("Failed processing DELETE request.", exc);

                WriteResponse(string.Format("Error in DELETE: {0}", exc.Message));
                errHandler.ErrorMessage = exc.Message;
            }
        }

        #endregion

        #region Utility Functions
        /// <summary>
        /// Method - Writes into the Response stream
        /// </summary>
        /// <param name="strMessage"></param>
        private static void WriteResponse(string strMessage)
        {
            HttpContext.Current.Response.Write(strMessage);
        }
        /// <summary>
        /// To convert a Byte Array of Unicode values (UTF-8 encoded) to a complete String.
        /// </summary>
        /// <param name="characters">Unicode Byte Array to be converted to String</param>
        /// <returns>String converted from Unicode Byte Array</returns>
        private String UTF8ByteArrayToString(Byte[] characters)
        {
            UTF8Encoding encoding = new UTF8Encoding();
            String constructedString = encoding.GetString(characters);
            return (constructedString);
        }
        /// <summary>
        /// Method - Serialize Class to XML
        /// </summary>
        /// <param name="emp"></param>
        /// <returns></returns>
        private String Serialize(object obj)
        {
            try
            {
                String XmlizedString = null;
                XmlSerializer xs = new XmlSerializer(typeof(object));
                //create an instance of the MemoryStream class since we intend to keep the XML string 
                //in memory instead of saving it to a file.
                MemoryStream memoryStream = new MemoryStream();
                //XmlTextWriter - fast, non-cached, forward-only way of generating streams or files 
                //containing XML data
                XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
                //Serialize emp in the xmlTextWriter
                xs.Serialize(xmlTextWriter, obj);
                //Get the BaseStream of the xmlTextWriter in the Memory Stream
                memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
                //Convert to array
                XmlizedString = UTF8ByteArrayToString(memoryStream.ToArray());
                return XmlizedString;
            }
            catch (Exception ex)
            {
                errHandler.ErrorMessage = ex.Message.ToString();
                throw;
            }

        }
        #endregion
    }


}
