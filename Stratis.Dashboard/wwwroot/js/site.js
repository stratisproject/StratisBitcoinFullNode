/**
 * Resize function without multiple trigger
 * 
 * Usage:
 * $(window).smartresize(function(){  
 *     // code here
 * });
 */
(function($, sr) {
   // debouncing function from John Hann
   // http://unscriptable.com/index.php/2009/03/20/debouncing-javascript-methods/
   var debounce = function(func, threshold, execAsap) {
      var timeout;

      return function debounced() {
         var obj = this, args = arguments;
         function delayed() {
            if (!execAsap)
               func.apply(obj, args);
            timeout = null;
         }

         if (timeout)
            clearTimeout(timeout);
         else if (execAsap)
            func.apply(obj, args);

         timeout = setTimeout(delayed, threshold || 100);
      };
   };

   // smartresize 
   jQuery.fn[sr] = function(fn) { return fn ? this.bind('resize', debounce(fn)) : this.trigger(sr); };

})(jQuery, 'smartresize');



var CURRENT_URL = window.location.href.split('#')[0].split('?')[0],
    $BODY = $('body'),
    $MENU_TOGGLE = $('#menu_toggle'),
    $SIDEBAR_MENU = $('#sidebar-menu'),
    $SIDEBAR_FOOTER = $('.sidebar-footer'),
    $LEFT_COL = $('.left_col'),
    $RIGHT_COL = $('.right_col'),
    $NAV_MENU = $('.nav_menu'),
    $FOOTER = $('footer');




$(document).ready(function() {
   function setupComponentRefresh() {
      var container = $("#topPeers");

      var refreshComponent = function() {
         $.get("/Home/TopPeers/2", function(data) { container.html(data); });
      };

      $(function() { window.setInterval(refreshComponent, 5000); });
      refreshComponent();
   }


   function setupBandStatistics() {
      var container = $("#bandStatistics");

      var refreshComponent = function() {
         $.get("/Home/BandStatistics", function(data) {
            container.html(data);
         });
      };

      $(function() { window.setInterval(refreshComponent, 30000); });
      refreshComponent();
   }



   setupComponentRefresh();

   setupBandStatistics();
});






function renderBandStatisticsChart(componentId, bandStatisticsData) {
   if (typeof (Chart) === 'undefined') { return; }

   var chartElement = $(componentId + ' .bandStatisticsChart').first();

   if (chartElement !== null) {
      var chartSettings = {
         type: 'doughnut',
         tooltipFillColor: "rgba(51, 51, 51, 0.55)",
         data: {
            labels: [
               "Received",
               "Sent"
            ],
            datasets: [{
               data: [bandStatisticsData.Received, bandStatisticsData.Sent],
               backgroundColor: [
                  "#3498DB",
                  "#26B99A"
               ],
               hoverBackgroundColor: [
                  "#49A9EA",
                  "#36CAAB"
               ]
            }]
         },
         options: {
            legend: false,
            responsive: false
         }
      }

      new Chart(chartElement, chartSettings);
   }
}