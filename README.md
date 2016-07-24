# NVM for Windows

## What is this???

So lot's of people absolutely love [NVM](https://github.com/creationix/nvm) on
\*nix systems. It's an amazing tool that makes node.js development much easier.
There has never been an alternative on Windows with the same feature set (that
  at least for me) makes NVM so good.

Namely the following:
* Zero setup, install and it "Just Works"
* No UAC, it shouldn't require Admin permission just to change node.js version
* Simultaneously use two node versions in different CMD windows
* Persist global modules across node.js versions
* Allow you to install based on major version numbers (`nvm install 6`)

## So did you do it??

Short answer: Yes

## So how do I use it??

Weren't you reading above

> Zero setup, install and it "Just Works"

You can find the installers over at our [Releases Page](https://github.com/MarshallOfSound/NVM-for-Windows/releases)  
You should uninstall your existing Node.JS installations before trying to use this.

## Usage

Usage is basically identical to the \*nix version

### Installing Versions

```bash
nvm install [version]
nvm install 6.3.0
nvm install 4
nvm install latest
```

### Using Versions

```bash
nvm use [version]
nvm use 6.3.0
nvm use 4
nvm use latest
```

**NOTE:** `nvm use latest` will use the latest installed version of node.js.  
Not the latest available online.

### Listing Versions

```bash
nvm list # All local versions
nvm list --remote # All remote versions
```
