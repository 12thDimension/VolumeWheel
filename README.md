VolumeWheel is a diy Project to build your own *physical* Volume Control with just an Arduino, four Rotary Encoders and a couple cables.

Warning: The Project is Windows Only


Before compiling make sure to insert your apps that you want to control in the "isCorrectSelectedApp" method in the class Form1.cs. 
This is very clunky for now as this is my first C# Project.

More documentation will follow. If you have a 3D Printer or a friend with one, Im currently developing a housing for the Arduino and rotary encoders and will provide the stl files as well.
**Please just ask if you want to do this too and need help**

Pull Requests appreciated :)

## Arduino Code
 ```
#include <Encoder.h>

Encoder myEnc1(3,4);
Encoder myEnc2(6,7);
Encoder myEnc3(9,10);
Encoder myEnc4(12,13);

const int buttonPin1 = 2;
const int buttonPin2 = 5;
const int buttonPin3 = 8;
const int buttonPin4 = 11;

long oldPosition1  = -999;
long oldPosition2  = -999;
long oldPosition3  = -999;
long oldPosition4  = -999;

void setup() {
  pinMode(buttonPin1, INPUT_PULLUP);
  pinMode(buttonPin2, INPUT_PULLUP);
  pinMode(buttonPin3, INPUT_PULLUP);
  pinMode(buttonPin4, INPUT_PULLUP);
  Serial.begin(9600);
}

void loop() {
  if (Serial.available() > 0) {
    String receivedCommand = Serial.readStringUntil('\n');
    receivedCommand.trim();
    
    if (receivedCommand == "VolumeWheel Arduino Check") {
      Serial.println("VolumeWheel Arduino Online");
    }
  }
  long newPosition1 = myEnc1.read();
  long newPosition2 = myEnc2.read();
  long newPosition3 = myEnc3.read();
  long newPosition4 = myEnc4.read();

  if (newPosition1 != oldPosition1) {
    oldPosition1 = newPosition1;
    Serial.print("0:");
    Serial.println(newPosition1);
  }

  if (newPosition2 != oldPosition2) {
    oldPosition2 = newPosition2;
    Serial.print("1:");
    Serial.println(newPosition2);
  }

  if (newPosition3 != oldPosition3) {
    oldPosition3 = newPosition3;
    Serial.print("2:");
    Serial.println(newPosition3);
  }

  if (newPosition4 != oldPosition4) {
    oldPosition4 = newPosition4;
    Serial.print("3:");
    Serial.println(newPosition4);
  }

  if (digitalRead(buttonPin1) == LOW) {
    Serial.println("0::P");
    while(digitalRead(buttonPin1) == LOW){
    }
    Serial.println("0::R");
  }
  if (digitalRead(buttonPin2) == LOW) {
    Serial.println("1::P");
    while(digitalRead(buttonPin2) == LOW){
    }
    Serial.println("1::R");
  }
  if (digitalRead(buttonPin3) == LOW) {
    Serial.println("2::P");
    while(digitalRead(buttonPin3) == LOW){
    }
    Serial.println("2::R");
  }
  if (digitalRead(buttonPin4) == LOW) {
    Serial.println("3::P");
    while(digitalRead(buttonPin4) == LOW){
    }
    Serial.println("3::R");
  }
}
```
