xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
dcl_texcoord0 r0
/*    0.0 */       alloc colors
/*    0.1 */       exec
/*    2   */          dp4 r2.x___, r0.wzxy, c1.wzxy
/*    3   */          dp3 r2._y__, r0.zxyy, c5.zxyy
/*    4   */          dp4 r2.__z_, r0.wzxy, c0.wzxy
/*    5   */          dp4 r1._y__, r0.wzxy, c2.wzxy
/*    6   */          dp3 r1.x___, r0.zxyy, c6.zxyy
/*    7   */          dp3 r2.___w, r0.zxyy, c4.zxyy
              +       maxs r0._, r1.yy
/*    1.0 */       exece
/*    8   */          dp4 oC0.___w, r0.wzxy, c3.wzxy
/*    9   */          add oC0.xy__, r2.zxxx, r2.wyyy
              +       adds_prev oC0.__z_, r1.x
/*    1.1 */       cnop
