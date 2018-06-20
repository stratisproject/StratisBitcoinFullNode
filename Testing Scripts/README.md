
Testing
===============


Federation Local Setup
-----------

These few files will help you create a Federation on your local machine.
For more information on what every entity involved in the Federation do, consult the [docs]().

0. Ideally, you need to have the latest version of the code.
1. Open federation.ps1 and update the 5 values at the top so that they match your environment.
  `$root_datadir` is where you want all the files relating to your federation will be downloaded.
  `$path_to_stratis_wallet_with_funds` is the path to a Stratis Testnet wallet you can use to deposit funds into the sidechain.
  The other ones are self-explanatory.
  The federation members' public keys, the multisig addresses, the ports are all set up so you don't have to do anything.
2. The first time the script is runs it'll try to copy the data folder from the StratisTestnet you may have on your machine.  This will save you downloading the whole blockchain many times.  So make sure your local copy of Stratis Test is up-to-date.
3. Run the script and give the nodes a few minutes to start and get going.
4. Import the file [Federation-setup-calls.saz](Federation-setup-calls.saz) into [Fiddler](https://www.telerik.com/fiddler).
5. Execute all the calls contained in the session above.  This will import the members mnemonics and passwords into their federation gateway nodes.
6. Import the [Federation.postman_collection.json](Federation.postman_collection.json) file into [Postman](https://www.getpostman.com/) if you want to make calls to deposit and withdraw funds.

In addition, [Shut-down-nodes.saz](Shut-down-nodes.saz) has a list of calls that stops all the nodes.

