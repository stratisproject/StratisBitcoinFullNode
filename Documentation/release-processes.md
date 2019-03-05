## How we do releases ##

Many users of a blockchain often compile from source and run master directly, that’s why we try to keep master as stable as possible.
However, to guarantee a stable version its recommended to run a release, a release branch normally goes through extensive testing.

Our branching strategy is simple

![image](https://user-images.githubusercontent.com/7487930/53817018-8dcbde00-3f5c-11e9-853f-ff1a997c25cc.png)

Devs commit regularly to master and when a set of features are agreed on a release branch is created and we increment masters version.
The reason master is incremented is that users often run master itself, that way versions of master on the network are also represented. 

**Release steps check list:**

1. Create a release branch.
2. Increment master’s version.
3. The release branch goes in to regression testing, any bugs are fixed.
4. The release branch is tested for the specific new features, any bugs are fixed on the release branch.
5. Hot fixes are merged back to master.
6. Release binaries are published.
7. Remove the `beta` tag and publish nuget packages.
8. Create a release tag.
9. Delete the release branch (optional).


