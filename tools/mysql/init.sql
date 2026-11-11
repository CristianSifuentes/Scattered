USE `scattered`;

CREATE TABLE `Products` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `Name` VARCHAR(150) NOT NULL,
            `Description` VARCHAR(150) NOT NULL,
            PRIMARY KEY (`Id`)
) AUTO_INCREMENT = 1;

INSERT INTO `scattered`.`Products` (`Id`, `Name`, `Description`) VALUES ('1', 'Producto 1', 'La descripción dice qu es el primer producto');
INSERT INTO `scattered`.`Products` (`Id`, `Name`, `Description`) VALUES ('2', 'Segundo producto', 'Este es el producto numero 2');
INSERT INTO `scattered`.`Products` (`Id`, `Name`, `Description`) VALUES ('3', 'Tercer', 'Terceras Partes nunca fueron buenas');
