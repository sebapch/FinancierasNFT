﻿
using Ipfs.Http;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MintearTest
{
    class Program
    {
        public partial class MintNFTFunction : MintNFTFunctionBase { }

        [Function("mintNFT", "bytes32")]
        public class MintNFTFunctionBase : FunctionMessage
        {
            [Parameter("address", "_recipient", 1)]
            public virtual string Recipient { get; set; }
            [Parameter("string", "_tokenURI", 2)]
            public virtual string TokenURI { get; set; }
            [Parameter("tuple", "_metadata", 3)]
            public virtual Metadata Metadata { get; set; }
        }

        public partial class Metadata : MetadataBase { }

        public class MetadataBase
        {
            [Parameter("uint32", "autonumeriId", 1)]
            public virtual uint autonumeriId { get; set; }
            [Parameter("uint32", "loanId", 2)]
            public virtual uint LoanId { get; set; }
            [Parameter("uint32", "numeroDeCliente", 3)]
            public virtual uint NumeroDeCliente { get; set; }
            [Parameter("uint32", "fintechId", 4)]
            public virtual uint FintechId { get; set; }
            [Parameter("string", "fechaDeCreacion", 5)]
            public virtual string FechaDeCreacion { get; set; }
            [Parameter("string", "uriImagen", 6)]
            public virtual string UriImagen { get; set; }
            [Parameter("bytes32", "hashRegistroBase", 7)]
            public virtual byte[] HashRegistroBase { get; set; }
        }
        static void Main(string[] args)
        {

            var loanId = 10;
            var nroCliente = 1;
            var loanDate = DateTime.Now;
            var fintechId = 2;

            //PDF TO IPFS
            var pdf = File.ReadAllBytes(@"Files\contrato.pdf");

            var pdfHash = subirIpfs(pdf);

            Console.WriteLine("Pdf IPFS Hash: " + pdfHash);

            //IMAGE TO IPFS
            var logo = Image.FromFile(@"Files\logo.jpg");

            var logoHash = subirIpfs(writeLogo(logo, loanId));

            Console.WriteLine("Logo IPFS Hash: " + logoHash);

            //GENERATE NFT JSON
            var solseaJson = File.ReadAllText(@"Files\solsea_template_json.txt");

            var nftName = "TeBancamos LOAN " + loanId + $" - {loanDate.ToShortDateString()} {loanDate.Hour}:{loanDate.Minute.ToString().PadLeft(2, '0')}:{loanDate.Second.ToString().PadLeft(2, '0')}";
            var nftDescription = "I am a description!";

            solseaJson = solseaJson
                        .Replace("{{NAME}}", nftName)
                        .Replace("{{DESCRIPTION}}", nftDescription)
                        .Replace("{{IMAGE_HASH}}", logoHash)
                        .Replace("{{LOAN_LOANID}}", loanId.ToString())
                        .Replace("{{LOAN_NROCLIENT}}", nroCliente.ToString())
                        .Replace("{{LOAN_DATE}}", loanDate.ToString("dd-mm-yyyy"))
                        .Replace("{{LOAN_FINTECHID}}", fintechId.ToString());

            var jsonHash = subirIpfs(Encoding.UTF8.GetBytes(solseaJson));

            Console.WriteLine(solseaJson);
            Console.WriteLine("NFT Json IPFS Hash: " + jsonHash);

            //MINTEAR
            Console.WriteLine("Starting Minting...");

            var metadata = new Metadata
            {
                autonumeriId = 24,
                LoanId = (uint)loanId,
                NumeroDeCliente = (uint)nroCliente,
                HashRegistroBase = HexByteConvertorExtensions.HexToByteArray("0x87d43F463118cbcaC8Cf9F31f2C824dE02C3AD8E"),
                FintechId = (uint)fintechId,
                UriImagen = "hola",
                FechaDeCreacion = loanDate.ToShortDateString()
            };

            var recipient = "0x87d43F463118cbcaC8Cf9F31f2C824dE02C3AD8E"; //Address financiera

            mintearNft(metadata, recipient, jsonHash);

            Console.WriteLine("Finish!");
            Console.ReadKey();
        }

        public static string subirIpfs(byte[] archivo)
        {
            MemoryStream stream = new MemoryStream(archivo);

            var ipfs = new IpfsClient(@"https://ipfs.infura.io:5001");

            var result = ipfs.FileSystem.AddAsync(stream).Result;
            
            return result.Id.Hash.ToString();
        }

        public static byte[] writeLogo(Image logo, int number)
        {
            PointF firstLocation = new PointF(20f, 20f);

            Bitmap newBitmap;
            using (var bitmap = (Bitmap)logo)
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    using (Font arialFont = new Font("Arial", 40))
                    {
                        graphics.DrawString(number.ToString(), arialFont, new SolidBrush(Color.FromArgb(255,255,255)), firstLocation);
                    }
                }
                newBitmap = new Bitmap(bitmap);
            }

            using (var stream = new MemoryStream())
            {
                newBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);

                newBitmap.Save("logoEscrito.png");
                newBitmap.Dispose();

                return stream.ToArray();
            }
        }

        public static async void mintearNft(Metadata metadata, string recipient, string tokenURI)
        {
            //INIT WEB3
            var privateKey = "0x9628316996948cf66bed79e797f9a5737a456ce478777454d2bd9df250c03bc3";
            var account = new Account(privateKey, Nethereum.Signer.Chain.Rinkeby);
            var url = "https://rinkeby.infura.io/v3/9aa3d95b3bc440fa88ea12eaa4456161";
            var web3 = new Web3(account, url);

            //GET CONTRACT
            var contractAddress = "0x3EFbab2e1675fd3397C28A8CC82Be4aee0d574E5";
            var contractHandler = web3.Eth.GetContractHandler(contractAddress);

            //CALL CONTRACT FUNCTION
            var mintNFTFunction = new MintNFTFunction
            {
                Recipient = recipient,  //Receptor del NFT
                TokenURI = tokenURI,    //Json del nft 
                Metadata = metadata    //Metadata publica  
            };

            var mintNFTFunctionTxnReceipt = contractHandler.SendRequestAndWaitForReceiptAsync(mintNFTFunction).Result;

            var response = JsonConvert.SerializeObject(mintNFTFunctionTxnReceipt);
            Console.WriteLine(response);

            Console.Read();
        }
    }
}