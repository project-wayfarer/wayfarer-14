reagent-effect-condition-guidebook-species-type-empty =
    the metabolizing body is a humanoid

reagent-effect-condition-guidebook-species-type-whitelist =
    the metabolizing body is {INDEFINITE($species)} {$species}

reagent-effect-condition-guidebook-species-type-blacklist =
    the metabolizing body is a humanoid, but not {INDEFINITE($species)} {$species}

reagent-effect-condition-guidebook-species-type-species = {$species}

reagent-effect-condition-guidebook-blood-reagent-threshold =
    { $max ->
        [2147483648] there's at least {NATURALFIXED($min, 2)}u of {$reagent}
        *[other] { $min ->
                    [0] there's at most {NATURALFIXED($max, 2)}u of {$reagent}
                    *[other] there's between {NATURALFIXED($min, 2)}u and {NATURALFIXED($max, 2)}u of {$reagent}
                 }
    }
