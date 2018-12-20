const Client = require('bitcoin-core');

const client = new Client({
    host: 'localhost',
    port: 24334,
    username: 'rpcuser',
    password: 'rpcpassword'
});

client.getInfo().then((data, err) => {
    if (err) return console.error(err);

    console.log(' info: ', data);

    runTests();
});

// REQUIRED
// getnewaddress
// validateaddress
// getblockcount
// sendtoaddress
// getwalletinfo
// gettransaction

function runTests() {
    // client.getNewAddress(null, 'legacy').then((data, err) => {
    //     if (err) return console.error(err);

    //     console.log('');
    //     console.log(' getnewaddress: ', data);
    // });

    // TEST NET ADDRESS
    // client.validateAddress('TarjoWiUjNNrXm6z2T7dB6aZL8T6AhxyML').then((data, err) => {
    //     console.log('');
    //     console.log(' validateAddress (TEST NET): ', data);
    // });

    // MAIN NET ADDRESS 
    // client.validateAddress('CGdxNqvBa74U34saF18G8bUqvWxEGcGsza').then((data, err) => {
    //     console.log('');
    //     console.log('validateAddress (MAIN): ', data);
    // });

    // client.getBlockCount().then((data, err) => {
    //     console.log('');
    //     console.log(' getblockcount: ', data);
    // });

    // REPLACE WITH TRANSACTION IN YOUR OWN WALLET:
    // client.getTransaction('d78c4155c09dbdd36daedb730d2d9caed825d0b1e331f6ecbb69c45b7c3b17ff').then((data, err) => {
    //     console.log('');
    //     console.log(' gettransaction: ', data);
    // });

    // HAS DETAILS, IS RECEIVE TRANSACTION
    // client.getTransaction('58d76a57f57a98748563c7389d369205e946246692863506da2551425a005dc5').then((data, err) => {
    //     console.log('');
    //     console.log(' gettransaction: ', data);
    // });

    // WALLET: DEFAULT
    function testDefaultWallet() {
        client.getTransaction('58d76a57f57a98748563c7389d369205e946246692863506da2551425a005dc5').then((data, err) => {
            console.log('');
            console.log(' gettransaction (1): ', data);

            client.getTransaction('7799fd7f0c1aabc30286f1ebb28b1065ff01fb8edb8843cb0952a8d543ac71d2').then((data, err) => {
                console.log('');
                console.log(' gettransaction (2): ', data);

                client.getTransaction('36dd085cdd0b7e795002c635c2973fe315a3f0faeb94a428216ad4b446f06c94').then((data, err) => {
                    console.log('');
                    console.log(' gettransaction (3): ', data);

                    client.getTransaction('c67caafc2efca79b3bf764e7297fd5e51fc02221e2594bad2ff3efd4f5fe0692').then((data, err) => {
                        console.log('');
                        console.log(' gettransaction (4): ', data);

                        client.getTransaction('9946c4549b1180735026f72812869d572e0a94fb0ab876d43f70466de7d719da').then((data, err) => {
                            console.log('');
                            console.log(' gettransaction (5): ', data);
                        });
                    });
                });
            });
        });
    }


    // WALLET: STAKING
    function testStakingWallet() {
        client.getTransaction('1840833aa6c2d86130e583e7e28a98cc9dae699342760080a7357b0393fc1ae2').then((data, err) => {
            console.log('');
            console.log(' gettransaction (1): ', data);
    
            client.getTransaction('a0fc5b375dbc0893f94ed1c9615b665b7c9d74a92e2412d07526df9305f3ade3').then((data, err) => {
                console.log('');
                console.log(' gettransaction (2): ', data);
    
                client.getTransaction('d1b2b8020e0fc05b01c6676f772f442acd46c7d86033e5ed048126949025b39a').then((data, err) => {
                    console.log('');
                    console.log(' gettransaction (3): ', data);
    
                    client.getTransaction('bf559a224fb0ff039f6517debfd0d186e5e70f8eea086a3ab6cf771689c732a5').then((data, err) => {
                        console.log('');
                        console.log(' gettransaction (4): ', data);
    
                        client.getTransaction('0d6b1f9bb70c8ae03147845f9be9c1144542242bfa50f8c17220705c5c672a2d').then((data, err) => {
                            console.log('');
                            console.log(' gettransaction (5): ', data);
                        });
                    });
                });
            });
        });
    }

    
    // WALLET: 1
    function testFunWallet() {
        client.getTransaction('d78c4155c09dbdd36daedb730d2d9caed825d0b1e331f6ecbb69c45b7c3b17ff').then((data, err) => {
            console.log('');
            console.log(' gettransaction (1): ', data);
    
            client.getTransaction('86d5fb3bdcc1b4b8ce5dcdb300d76ba98992251500f09f25684c68744422ec12').then((data, err) => {
                console.log('');
                console.log(' gettransaction (2): ', data);
    
                client.getTransaction('1fc15ea595b0377987abb85da2da9333a3f262de143dec5c0561019854d4923a').then((data, err) => {
                    console.log('');
                    console.log(' gettransaction (3): ', data);
    
                    client.getTransaction('788a6c4c82d2783b2cac7af438bf5896fae0e565915f92e4c64241b9e105caa1').then((data, err) => {
                        console.log('');
                        console.log(' gettransaction (4): ', data);
    
                        client.getTransaction('7b3a9f7942277f0df0aab7da0eb0ea3e8c58fb460041bf4e7371d4055c042e4b').then((data, err) => {
                            console.log('');
                            console.log(' gettransaction (5): ', data);

                            client.getTransaction('8f1177fefc2c1fd605acfd0f5355db048d6a1403ebc56e88920bfb2e1020e1d1').then((data, err) => {
                                console.log('');
                                console.log(' gettransaction (5): ', data);
                            });
                            
                        });
                    });
                });
            });
        });
    }

    //testDefaultWallet();

    // client.getWalletInfo().then((data, err) => {
    //     console.log('');
    //     console.log(' getwalletinfo: ', data);
    // });

    // SEND TO ADDRESS + VERIFY TRANSACTION (use default wallet)
    // client.sendToAddress('Tt94Ht4NNkDZvkhjcMiUZHokLERUm5UJQx', 5).then((data, err) => {
    //     console.log('');
    //     console.log(' sendtoaddress: ', data);
    //     client.getTransaction(data).then((data, err) => {
    //         console.log('');
    //         console.log(' gettransaction: ', data); // DOES NOT CONTAIN DETAILS, AS IT WAS SENT, NOT RECEIVED.
    //     });
    // });

    // UNLOCK WALLET + SEND TO ADDRESS + VERIFY TRANSACTION
    // client.walletPassphrase('mypass', 60).then((data, err) => {
    //     console.log('');
    //     console.log(' walletPassPhrase: ', data);

    //     client.sendToAddress('Tn8WsjE2pLYAKm6JGypRsTJBKNuvoos9BK', 21).then((data, err) => {
    //         console.log('');
    //         console.log(' sendtoaddress: ', data);

    //         client.getTransaction(data).then((data, err) => {
    //             console.log('');
    //             console.log(' gettransaction: ', data);
    //         });

    //     });
    // });

}