#include "dio.h"
#include "sbus.h"
#include <assert.h>
#include <string.h>
#include <stdio.h>
#include <stdlib.h>
 
#define DIO_Z 2
 
/*******************************************************************************
* setdiopin: accepts a DIO register and value to place in that DIO pin.
*   Values can be 0 (low), 1 (high), or 2 (z - high impedance).
*******************************************************************************/
void setdiopin(int pin, int val)
{
   int pinOffSet;
   int dirPinOffSet; // For Register 0x66 only
   int outPinOffSet; // For Register 0x66 only
 
   // First, check for the high impedance case
   if (val == DIO_Z)
   {
      if (pin <= 40 && pin >= 37)
      {
         dirPinOffSet = pin - 33;
         sbus_poke16(0x66, sbus_peek16(0x66) & ~(1 << dirPinOffSet));
      }
      else if (pin <= 36 && pin >= 21)
      {
         pinOffSet = pin - 21;
         sbus_poke16(0x6c, sbus_peek16(0x6c) & ~(1 << pinOffSet));
      }
      else if (pin <= 20 && pin >= 5)
      {
         pinOffSet = pin - 5;
         sbus_poke16(0x72, sbus_peek16(0x72) & ~(1 << pinOffSet));
      }
   }
 
   /******************************************************************* 
   *0x66: DIO and tagmem control (RW)
   *  bit 15-12: DIO input for pins 40(MSB)-37(LSB) (RO)
   *  bit 11-8: DIO output for pins 40(MSB)-37(LSB) (RW)
   *  bit 7-4: DIO direction for pins 40(MSB)-37(LSB) (1 - output) (RW)
   ********************************************************************/
   else if (pin <= 40 && pin >= 37)
   {
      dirPinOffSet = pin - 33; // -37 + 4 = Direction; -37 + 8 = Output
      outPinOffSet = pin - 29;
 
      // set bit [pinOffset] to [val] of register [0x66] 
      if(val)
         sbus_poke16(0x66, (sbus_peek16(0x66) | (1 << outPinOffSet)));
      else
         sbus_poke16(0x66, (sbus_peek16(0x66) & ~(1 << outPinOffSet)));
 
      // Make the specified pin into an output in direction bits
      sbus_poke16(0x66, sbus_peek16(0x66) | (1 << dirPinOffSet)); ///
 
   }
 
   /********************************************************************* 
   *0x68: DIO input for pins 36(MSB)-21(LSB) (RO)    
   *0x6a: DIO output for pins 36(MSB)-21(LSB) (RW)
   *0x6c: DIO direction for pins 36(MSB)-21(LSB) (1 - output) (RW)
   *********************************************************************/
   else if (pin <= 36 && pin >= 21)
   {
      pinOffSet = pin - 21;
 
      // set bit [pinOffset] to [val] of register [0x6a] 
      if(val)
         sbus_poke16(0x6a, (sbus_peek16(0x6a) | (1 << pinOffSet)));
      else
         sbus_poke16(0x6a, (sbus_peek16(0x6a) & ~(1 << pinOffSet)));
 
      // Make the specified pin into an output in direction register
      sbus_poke16(0x6c, sbus_peek16(0x6c) | (1 << pinOffSet)); ///
   }
 
   /********************************************************************* 
   *0x6e: DIO input for pins 20(MSB)-5(LSB) (RO)    
   *0x70: DIO output for pins 20(MSB)-5(LSB) (RW)
   *0x72: DIO direction for pins 20(MSB)-5(LSB) (1 - output) (RW)
   *********************************************************************/
   else if (pin <= 20 && pin >= 5)
   {
      pinOffSet = pin - 5;
 
      if(val)
         sbus_poke16(0x70, (sbus_peek16(0x70) | (1 << pinOffSet)));
      else
         sbus_poke16(0x70, (sbus_peek16(0x70) & ~(1 << pinOffSet)));
 
      // Make the specified pin into an output in direction register
      sbus_poke16(0x72, sbus_peek16(0x72) | (1 << pinOffSet));
   }
 
}
 
/*******************************************************************************
* getdiopin: accepts a DIO pin number and returns its value.  
*******************************************************************************/
int getdiopin(int pin)
{
   int pinOffSet;
   int pinValue = 99999;
 
   /******************************************************************* 
   *0x66: DIO and tagmem control (RW)
   *  bit 15-12: DIO input for pins 40(MSB)-37(LSB) (RO)
   *  bit 11-8: DIO output for pins 40(MSB)-37(LSB) (RW)
   *  bit 7-4: DIO direction for pins 40(MSB)-37(LSB) (1 - output) (RW)
   ********************************************************************/
   if (pin <= 40 && pin >= 37)
   {
      pinOffSet = pin - 25; // -37 to get to 0, + 10 to correct offset
 
      // Obtain the specific pin value (1 or 0)
      pinValue = (sbus_peek16(0x66) >> pinOffSet) & 0x0001;
   }
 
   /*********************************************************************   
   *0x68: DIO input for pins 36(MSB)-21(LSB) (RO)  
   *0x6a: DIO output for pins 36(MSB)-21(LSB) (RW)
   *0x6c: DIO direction for pins 36(MSB)-21(LSB) (1 - output) (RW)
   *********************************************************************/
   else if (pin <= 36 && pin >= 21)
   {
      pinOffSet = pin - 21; // Easier to understand when LSB = 0 and MSB = 15
 
      // Obtain the specific pin value (1 or 0)
      pinValue = (sbus_peek16(0x68) >> pinOffSet) & 0x0001;
   }
 
   /*********************************************************************   
   *0x6e: DIO input for pins 20(MSB)-5(LSB) (RO)  
   *0x70: DIO output for pins 20(MSB)-5(LSB) (RW)
   *0x72: DIO direction for pins 20(MSB)-5(LSB) (1 - output) (RW)
   *********************************************************************/
   else if (pin <= 20 && pin >= 5)
   {
      pinOffSet = pin - 5;  // Easier to understand when LSB = 0 and MSB = 15
 
      // Obtain the specific pin value (1 or 0)
      pinValue = (sbus_peek16(0x6e) >> pinOffSet) & 0x0001;
   }
   return pinValue;
}