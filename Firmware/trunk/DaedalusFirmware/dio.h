/* 
 * File:   dio.h
 * Author: Dan
 *
 * Created on April 9, 2012, 8:29 PM
 */

#ifndef DIO_H
#define	DIO_H

#define RELAY_1                 39
#define RELAY_2                 37
#define RELAY_3                 35

#define RELAY_ACTIVE            1
#define RELAY_INACTIVE          0

#ifdef	__cplusplus
extern "C" {
#endif
    
int getdiopin(int pin);

void setdiopin(int pin, int val);

#ifdef	__cplusplus
}
#endif

#endif	/* DIO_H */

