## How we do releases ##

Many users of a blockchain often compile directly from the master branch in the repository, which is why we try to keep our master branch as stable as possible.
However, to guarantee a stable version, it is recommended to compile and run from a release branch because a release branch normally goes through extensive testing.

Our branching strategy is simple:

![image](https://user-images.githubusercontent.com/7487930/53817018-8dcbde00-3f5c-11e9-853f-ff1a997c25cc.png)

Developers make regular commits to the master branch. When a set of features is agreed on, a release branch is created, and we increment the master version.
The reason the master version is incremented is that, as mentioned previously, users often run from the master branch itself. This way master versions are also represented on the network. 

**Release steps check list:**

1. A release branch is created.
2. The master branch version is incremented.
3. The release branch goes in to regression testing and any bugs are fixed.
4. Specific new features on the release branch are tested, and any bugs are fixed on the release branch.
5. Hot fixes are merged back to the master branch.
6. The release binaries are published.
7. The nuget packages are published after removing the `beta` tag.
8. A release tag is created on github.
9. The release branch is deleted (optional).

