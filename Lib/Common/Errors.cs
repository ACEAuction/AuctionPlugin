using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Legend.Lib.Common.Errors
{
    public class AuctionFailure : Exception
    {
        private const string Prefix = "[AuctionFailure]";

        public AuctionFailure() : base($"{Prefix} Generic Auction Exception.") { }

        public AuctionFailure(string message)
            : base($"{Prefix} {message}") { }

        public AuctionFailure(string message, Exception innerException)
            : base($"{Prefix} {message}", innerException) { }
    }

    public class BankFailure : Exception
    {
        private const string Prefix = "[BankFailure]";

        public BankFailure()
            : base($"{Prefix} Generic Bank failure Exception.") { }

        public BankFailure(string message)
            : base($"{Prefix} {message}") { }

        public BankFailure(string message, Exception innerException)
            : base($"{Prefix} {message}", innerException) { }
    }

    public class ItemTransferFailure : Exception
    {
        private const string Prefix = "[ItemTransfer]";

        public ItemTransferFailure()
            : base($"{Prefix} Generic item transfer Exception.") { }

        public ItemTransferFailure(string message)
            : base($"{Prefix} {message}") { }

        public ItemTransferFailure(string message, Exception innerException)
            : base($"{Prefix} {message}", innerException) { }
    }
}
