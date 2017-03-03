using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Stratis.Dashboard.Model;

namespace Stratis.Dashboard.ViewComponents {
   public class MessagesViewComponent : ViewComponent {
      public async Task<IViewComponentResult> InvokeAsync() {
         var messages = GetFakeMessages();

         return View(messages);
      }



      private List<Message> GetFakeMessages() {
         return new List<Model.Message>() {
            new Message() {
               Date =DateTime.Now.AddDays(-1),
               From ="User Alpha",
               Text ="Hey, whatsaaaaaa"
            },
            new Message() {
               Date =DateTime.Now.AddDays(-1).AddHours(2),
               From ="User Beta",
               Text ="Bird is the word!"
            }
         };
      }
   }
}