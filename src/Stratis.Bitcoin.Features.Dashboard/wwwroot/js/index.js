function ViewModel() {
    this.status = ko.observable();
    var collection = ko.observable();
    this.getStatus = function () {
        var self = this;
        axios.get('http://localhost:37221/api/Node/status')
            .then(function (response) {
                self.status(response.data);
            })
            .catch(function (error) {
                console.log(error);
            });
    }

    this.update = function () {
        var self = this;
        setTimeout(function () {
            self.getStatus();
        }, 10000);
    }
    this.getStatus();
    this.update();
}
ko.applyBindings(new ViewModel()); 