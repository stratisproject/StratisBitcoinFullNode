import * as _jquery_ from "jquery";
import * as _angular_ from "angular";

function sayHello() {
   const compiler = (document.getElementById("compiler") as HTMLInputElement).value;
   const framework = (document.getElementById("framework") as HTMLInputElement).value;
   
   return `Hello from ${compiler} and ${framework}!`;
}



