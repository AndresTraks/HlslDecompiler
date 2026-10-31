xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
dcl_texcoord0 r0.xyz
/*    0.0 */       alloc colors
/*    0.1 */       exece
/*    1   */          dp3_sat r1.x___, r0.zxyy, c0.zxyy
/*    2   */          dp3_sat r1._y__, r0.zxyy, c2.zxyy
/*    3   */          dp3_sat r0.x___, r0.zxyy, c4.zxyy
/*    4   */          mul r0, r0.xxxx, c5
/*    5   */          mad r0, r1.yyyy, c3.xzwy, r0.xzwy
/*    6   */          mad oC0, r1.xxxx, c1, r0.xwyz
