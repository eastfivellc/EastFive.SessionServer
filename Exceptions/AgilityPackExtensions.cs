using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Security.SessionServer.Exceptions
{
    public static class AgilityPackExtensions
    {
        public static IEnumerable<HtmlNode> AsHtmlNodes(this HtmlNodeCollection nodes)
        {
            foreach(var node in nodes)
            {
                yield return node;
            }

        }
    }
}
