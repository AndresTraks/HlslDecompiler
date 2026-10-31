xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=0
def c248, 0, 0, 0, 0
def c249, 0, 0, 0, 0
def c250, 6.6, 8.2, 9.8, 11.4
def c251, 1.5, 2, 2.5, 3
def c252, 1.4000001, 1.8000001, 2.2, 2.6
def c253, 1.3000001, 1.6, 1.9, 2.2
def c254, 1.2, 1.4000001, 1.5999999, 1.8
def c255, 1.1, 1.2, 1.3, 1.4
dcl_texcoord0 r0
/*    0.0 */       alloc colors
/*    0.1 */       exece
/*    1   */          mad r0, r0, c0, c255
/*    2   */          mad r0, r0, c1, c254
/*    3   */          mad r0, r0, c2, c253
/*    4   */          mad r0, r0, c3, c252
/*    5   */          mad r0, r0, c4, c251
/*    6   */          mad oC0, r0, c5, c250
