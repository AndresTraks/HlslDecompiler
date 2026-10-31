xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=0
dcl_texcoord0 r0.xy
/*    0.0 */       exec
/*    2   */          tfetch2D r0._w__, r0.xyx, tf0
/*    0.1 */       alloc colors
/*    1.0 */       exece
/*    3   */          max oC0.000_, r0, r0
              +       mulsc oC0.___w, c0.x, r0.y
/*    1.1 */       cnop
