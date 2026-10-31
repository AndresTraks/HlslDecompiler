xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
dcl_texcoord0 r0.xy
/*    0.0 */       exec
/*    2   */          tfetch2D r1, r0.yx, tf1
/*    3   */          tfetch2D r2, r0.xyx, tf0, FetchValidOnly=false
/*    4   */          serialize
                      tfetch2D r0, r2.xyx, tf2
/*    0.1 */       alloc colors
/*    1.0 */       exece
/*    5   */          mad oC0, r2, r1, r0
/*    1.1 */       cnop
