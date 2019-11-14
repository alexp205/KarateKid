-- KarateKid by Alexander Politowicz
-- for use with Karate Champ (USA) (Rev A).nes

if gameinfo.getromname() == "Karate Champ" then
    button_names = {
        "A",
        "B",
        "Up",
        "Down",
        "Left",
        "Right",
        "Start",
    }
end

-- EXAMPLE
-- TODO: delete these
function getPositions()
    marioX = memory.read_s16_le(0x94)
    marioY = memory.read_s16_le(0x96)
        
    local layer1x = memory.read_s16_le(0x1A);
    local layer1y = memory.read_s16_le(0x1C);
        
    screenX = marioX-layer1x
    screenY = marioY-layer1y
end

function sigmoid(x)
    return 2/(1+math.exp(-4.9*x))-1
end

function clearJoypad()
    controller = {}
    for b = 1,#ButtonNames do
        controller["P1 " .. ButtonNames[b]] = false
    end
    joypad.set(controller)
end

function playTop()
    local maxfitness = 0
    local maxs, maxg
    for s,species in pairs(pool.species) do
        for g,genome in pairs(species.genomes) do
            if genome.fitness > maxfitness then
                maxfitness = genome.fitness
                maxs = s
                maxg = g
            end
        end
    end
    
    pool.currentSpecies = maxs
    pool.currentGenome = maxg
    pool.maxFitness = maxfitness
    forms.settext(maxFitnessLabel, "Max Fitness: " .. math.floor(pool.maxFitness))
    initializeRun()
    pool.currentFrame = pool.currentFrame + 1
    return
end
-- END EXAMPLE

-- TODO: change this
function onExit()
    forms.destroy(form)
end

event.onexit(onExit)

while true do
    -- TODO: change this
    local backgroundColor = 0xD0FFFFFF
    if not forms.ischecked(hideBanner) then
        gui.drawBox(0, 0, 300, 26, backgroundColor, backgroundColor)
    end

    joypad.set(controller)

    timeout = timeout - 1
    
    -- TODO: change this
    if not forms.ischecked(hideBanner) then
        gui.drawText(0, 0, "Gen " .. pool.generation .. " species " .. pool.currentSpecies .. " genome " .. pool.currentGenome .. " (" .. math.floor(measured/total*100) .. "%)", 0xFF000000, 11)
        gui.drawText(0, 12, "Fitness: " .. math.floor(rightmost - (pool.currentFrame) / 2 - (timeout + timeoutBonus)*2/3), 0xFF000000, 11)
        gui.drawText(100, 12, "Max Fitness: " .. math.floor(pool.maxFitness), 0xFF000000, 11)
    end
        
    pool.currentFrame = pool.currentFrame + 1

    emu.frameadvance();
end
