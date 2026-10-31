xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 0.5, 0, 1, 0
dcl_texcoord0 r0
/*    0.0 */       alloc colors
/*    0.1 */       exece
/*    1   */          sne r1, r0, c255.yyyy
/*    2   */          max4 r0._y__, -r1
/*    3   */          max4 r0.x___, r1
/*    4   */          cndeq r0.x___, r0.xxxx, c255.yyyy, c255.xxxx
/*    5   */          cndeq oC0, r0.yyyy, r0.xxxx, c255.zzzz
