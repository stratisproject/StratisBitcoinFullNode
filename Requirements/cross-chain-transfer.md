# Suggestion for Cross Chain transfers mechanism

## 1) All federation members nodes online

### A cross chain transfer is initiated
![A cross chain transfer is initiated](../assets/cross-chain-transfers/happy-path-1.svg)

### The cross chain transfer is triggered after MaxReorg
![The cross chain transfer is triggered after MaxReorg](../assets/cross-chain-transfers/happy-path-2.svg)

### The cross chain transfer appears on targeted chain
![The cross chain transfer appears on targeted chain](../assets/cross-chain-transfers/happy-path-3.svg)

## 2) Preferred leader offline

### A cross chain transfer is triggered while leader is offline
![A cross chain transfer is triggered while leader is offline](../assets/cross-chain-transfers/leader-offline-1.svg)

### The cross chain is handled one block later by next leader
![The cross chain is handled one block later by next leader](../assets/cross-chain-transfers/leader-offline-2.svg)

### The leader comes back online
![The leader comes back online](../assets/cross-chain-transfers/leader-offline-3.svg)
