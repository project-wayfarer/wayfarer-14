# Ship Submission Requirements
Your ship must have a clear, defined role and be designed around it's intended purpose.

Ships designed in such a way to promote stuffing every single department, lathe and feature into a single grid will not be accepted.

Ships may only have a single primary and secondary role.

Do not:
* Put weaponry/custom equipment/powerful items around the ship
* Put references to yourself, your characters, or your friend group or otherwise upon the ship.
* Ship Parts should be placed in a way that looks logical and rational, to avoid powergaming builds.
* Put free materials upon the ship beyond a small amount to get started
* Every part of the ship should be accessible and not contain ‘hidden rooms’ by default

## Basics and hard requirements.
**Your ship MUST meet these requirements at a minimum:**
Core:
* Be airtight
* A two-stage airlock system for entering and exiting the ship via docking port.
* Shuttle console, fax machine, holopad (ship)
* Gyroscope
* Gravity generator
* All parts of the ship are powered
* A minimum of 1 fire extinguisher, defibrillator, toolbox and EVA capable suit locker.
* LateJoin spawn point and warp point.
* Vacuum AtmosFixMarker on **any** tiles that are exposed to space
* Name Cameras properly for what they are looking at (in the SurveillanceCameraComponent)

Engineering:
* Generates enough power to run the ship, through portable generators or solar panel arrays
* Power systems (SMES, Substations, APCs) connected in a way that wires do not go under walls unless absolutely necessary
* No more than two basic SMES batteries, but these are not requireed.
* A single spare fuel locker
* Atmospherics system with connectors for oxygen and nitrogen tanks, gas mixer, air vents, scrubbers and functional waste venting. Atmos piping follows the same rules as wiring
* Firelocks and Air Alarms set up to prevent catastrophic venting in case of a hull breach
## Ship Design
#### Size and Shape

1.  Ensure your ship is not too large and is capable of docking at various stations

2.  Ensure your ship has a distinctive shape that is easily identifiable, avoid making squares.

3.  Diagonal tiles should be used with consideration for their unique effects on visual elements such as blocking line of sight and interactions. Ensure all walls link up properly.

4.  Your ship should include space for people to live and sleep in, typically called ‘dorms’


#### Visual Design

1.  We’re aiming for a SolarPunk aesthetic, so feel free to include these in your designs. Plants, flowers, solar arrays, comfort elements and similar.

2.  Use the minimum amount of lights possible, this adds more atmosphere to your ship and allows use of lighting from the characters.

3.  Add external lights to your ship, typically above thrusters.

4.  Feel free to add as many decals and decorations to your ship as you desire, as long as it has a clean aesthetic that you’d imagine a company would sell, we will likely approve it.

5.  Try not to use a single type of tile for the floors, break up rooms with distinct visual designs

6.  Avoid glass floors, they can be added but should be at a minimum

7.  Atmos Piping:
* Waste pipes are: Red: #990000
* Distro pipes are: Blue: #0055cc

## Ship pricing
First, get your ships size by using the command “gridtc [gridID]”

Second, get your ships value by using the command “appraisegrid [gridID]”

The tile count of the grid will be multiplied by 50, as every tile has that cost associated with it, after that add the size cost to the grid cost.

The primary role's main department (Science basics such as artifact scanner, terminal and research server) does not count towards ship pricing.

Secondary roles and subdepartments incur a 1.15x multiplier for each, so if you have a Science vessel with a robotics and anomaly subdepartment with a secondary role such as a medical bay, it would multiply your ships final cost by 1.45.

Ensure you are calculating price on a ship that is initialized and functioning in-game.

#### Example ship with a Science primary role, anomaly and robotics subdepartment, and a Medical secondary:
gridtc (255x50) = 12,750

appraisegrid = 64,000

12,750 + 64,000 = 76,750

76,750 x 1.45 = 111,287.5

Round off to the nearest thousand = 111,000

## Department and Role system
### Roles

Essential Roles are required before you can apply any of the subroles that are available for that department.

A ship can never have more than two "roles" and more than 4 subdepartments.

#### Subdepartments

Subdepartments are an extension of the ships primary or secondary role.

**You can optionally leave anything out from the lists, but these are a recommended set of equipment for their slots**
### Science
RD Server/Computer
Artifact Analyser/Computer
Protolathe/Autolathe
Circuit Printer
#### Anomalies Subdepartment:
Anomaly Generator
APE
Anomaly analysis gear
Anomaly vessel
#### Robotics Subdepartment:
Robotics / Mechfab
Partially or fully built borg/mech
### Medical
Medical beds
Med techfab
Stasis bed
Nanomed
#### Chemistry Subdepartment
Chem dispenser / chemmaster
Centrifuge
Grinder
Electrolyser
Sink
Drain
Hotplate
#### Cryogenics Subdepartment
Cryopod
Linked up to the ship atmos or it’s own gas canister
Freezer
Waste filters for cryo
#### Botany Subdepartment
Hydroponics trays (1-2)
Botany equipment
Sink
### CGP
APUs to support ship power (as few as possible)
IFF consoles
Long range radars
One small cell
An evidence locker
#### Brig Subdepartment:
Space for 2-3 Prisoners to reside upon the ship
Criminal records computer
#### Ship Weapons Subdepartment:
Ship canons / M-EMP
#### Investigation Subdepartment:
Criminal Records Computer
Interrogation Space
Evidence lockers
#### CGP Armoury Subdepartment:
An armoury with space for 3-4 officers
Some lockers with lowest grade hardsuits
Space access
### Outlaws
Mining / Plastitanium walls
Reinforced Plasma / Uranium / Plastitanium Windows
Outlaw specific thrusters (thrusterRogue)
IFF Console
#### Ship Weapons Subdepartment:
Cannons
### Support
Large area of space for Cargo
Conveyor belts
Opening space for building
#### Engineering Subdepartment:
Circuit Printer
Flatpack machine
Engineering Techfab
#### Atmospherics Subdepartment:
Gas deposit miners
Gaslocks
Portable gaslocks
Portable APC
### Service
Service Techfab
One free initial Subdepartment
#### Kitchen Subdepartment
Microwave, Deep Fryer, Grille
Hydroponics Trays (1-2)
Seed extractors
Hydroponics equipment
Sink
#### Service Bar Subdepartment
Booze/Soda dispensers
Hydroponics Trays (1-2)
Hydroponics equipment
Sink
#### Service Hydroponics Subdepartment
Hydroponics Trays (As many as you want)
Seed extractors
Hydroponics equipment
Sink
#### Service Roleplay Subdepartment.

Anything that doesn’t fall into any other category and is a space specifically designed purely for only roleplay.
