# Changes

As of GPL requirement, this document lists all noticeable changes from original project. Statements valid as of 2021/12/29.

## Project

* Updated .NET Framework version to **.NET Framework 4.8**.

## Code

* Make a lot of key configurations invisible outside assembly (design issue), and then make them properties

* Renames parameters to meet the naming standard

* Extracted assignment to pursuit peds variable from if to declaration in `PedManager`

* Removed redundant `else` in `PedManager`

* Remove unused XML types

## Logic

* Made `PedManager` use `Functions.IsPedACop` to determine cop instead of using `Relationship` to check

## API

* Remove verification

* Removed transport related API

* Removed arrest ped API

## Interface

* Made `PedManager` use help message to indicate stopped peds cannot be grabbed

* Made `PedManager` hints the player to dismiss them instead of issuing warnings

* Made `PedManager` use help message to indicate peds in vehicles cannot be grabbed

* Made `PedManager` use help message to indicate only humans can be grabbed

* Made `PedManager` use help message to indicate a taxi is already assigned

* Fix typos

* Use American English

## Features

* Deprecate transport ped to hospital option

* Deprecate instant arrest

* Move Taxi service to it's own class
