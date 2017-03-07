using Microsoft.AspNetCore.Mvc.Rendering;

namespace Stratis.Dashboard.HtmlHelpers {
   public static class PrettyfyExtension {
      public static string PrettySize(this IHtmlHelper helper, long size) {
         string postfix = "Bytes";
         long result = size;
         //more than 1 GB
         if (size >= 1073741824) {
            result = size / 1073741824;
            postfix = "GB";
         }
         //more that 1 MB
         else if (size >= 1048576) {
            result = size / 1048576;
            postfix = "MB";
         }
         //more that 1 KB
         else if (size >= 1024) {
            result = size / 1024;
            postfix = "KB";
         }

         return result.ToString("F1") + " " + postfix;
      }
   }
}
